using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// The look-development pass: post-processing, ambient light, fog and the
/// light rig, applied per scene with a mood of its own.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod CinematicLookBuilder.BuildFromCommandLine
///
/// Why this exists as a builder rather than as hand-editing in the Editor:
/// the scenes are ForceBinary-serialised and are regenerated wholesale by
/// FullSceneRebuild, so any look work done by hand is lost the next time
/// anything is rebuilt. Everything here is idempotent and re-runnable.
///
/// What was actually wrong before this pass, all of it found by rendering the
/// game and looking at it:
///
///   - No camera carried UniversalAdditionalCameraData, so URP's
///     renderPostProcessing defaulted to FALSE and *no* post-processing ran
///     at all - no tonemapping, no bloom, no vignette. This is the single
///     largest reason the game read as flat and cheap.
///   - Ambient was Flat at RGBA(0.06, 0.07, 0.10) with only a 0.5-intensity
///     moonlight, so interiors rendered essentially black.
///   - The URP asset was on LowDynamicRange grading with MSAA off, which
///     throws away the highlight range that bloom and ACES need.
///   - Fog was off entirely, so there was no depth cue at any distance.
/// </summary>
public static class CinematicLookBuilder
{
    private const string ProfileFolder = "Assets/Settings/Post";

    [MenuItem("Museum of Time/Build Cinematic Look")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    // ------------------------------------------------------------------
    // Per-scene mood definition
    // ------------------------------------------------------------------

    private struct Mood
    {
        public string scenePath;
        public string profileName;

        public Color ambientSky;
        public Color ambientEquator;
        public Color ambientGround;

        public bool fog;
        public Color fogColor;
        public float fogDensity;

        public float postExposure;
        public float contrast;
        public float saturation;

        public float bloomIntensity;
        public float bloomThreshold;
        public Color bloomTint;

        public float vignette;
        public float filmGrain;
        public float chromatic;
    }

    private static readonly Mood[] Moods =
    {
        // MuseumNight - moonlit marble, warm pooled exhibit spots.
        new Mood
        {
            scenePath = "Assets/Scenes/MuseumNight.unity",
            profileName = "Post_MuseumNight",
            // Lifted after the ceiling went in: the coffered roof blocks most
            // of the moonlight that used to flood the open-topped box, so the
            // room needs a little more ambient to stay readable without
            // losing the night-museum mood.
            ambientSky     = new Color(0.32f, 0.38f, 0.52f),
            ambientEquator = new Color(0.26f, 0.28f, 0.34f),
            ambientGround  = new Color(0.18f, 0.16f, 0.13f),
            fog = true,
            fogColor = new Color(0.09f, 0.11f, 0.18f),
            fogDensity = 0.007f,
            postExposure = 0.35f,
            contrast = 12f,
            saturation = -4f,
            bloomIntensity = 0.85f,
            bloomThreshold = 0.85f,
            bloomTint = new Color(1f, 0.92f, 0.78f),
            vignette = 0.34f,
            filmGrain = 0.18f,
            chromatic = 0.08f,
        },

        // FrozenCity - flat cold daylight through ice haze.
        new Mood
        {
            scenePath = "Assets/Scenes/FrozenCity.unity",
            profileName = "Post_FrozenCity",
            ambientSky     = new Color(0.34f, 0.42f, 0.55f),
            ambientEquator = new Color(0.26f, 0.31f, 0.39f),
            ambientGround  = new Color(0.18f, 0.20f, 0.24f),
            fog = true,
            fogColor = new Color(0.55f, 0.63f, 0.74f),
            fogDensity = 0.022f,
            postExposure = 0.2f,
            contrast = 8f,
            saturation = -18f,
            bloomIntensity = 1.15f,
            bloomThreshold = 0.8f,
            bloomTint = new Color(0.82f, 0.92f, 1f),
            vignette = 0.28f,
            filmGrain = 0.14f,
            chromatic = 0.06f,
        },

        // ClockCore - the machine at the end of time. Dark and dramatic.
        new Mood
        {
            scenePath = "Assets/Scenes/ClockCore.unity",
            profileName = "Post_ClockCore",
            ambientSky     = new Color(0.26f, 0.23f, 0.36f),
            ambientEquator = new Color(0.21f, 0.18f, 0.27f),
            ambientGround  = new Color(0.14f, 0.11f, 0.14f),
            fog = true,
            fogColor = new Color(0.10f, 0.08f, 0.14f),
            fogDensity = 0.010f,
            postExposure = 0.4f,
            contrast = 18f,
            saturation = -6f,
            bloomIntensity = 1.4f,
            bloomThreshold = 0.75f,
            bloomTint = new Color(1f, 0.85f, 0.6f),
            vignette = 0.42f,
            filmGrain = 0.22f,
            chromatic = 0.12f,
        },

        // MainMenu - the same museum look, pushed a little more filmic.
        new Mood
        {
            scenePath = "Assets/Scenes/MainMenu.unity",
            profileName = "Post_MainMenu",
            ambientSky     = new Color(0.18f, 0.22f, 0.34f),
            ambientEquator = new Color(0.13f, 0.14f, 0.18f),
            ambientGround  = new Color(0.10f, 0.08f, 0.06f),
            fog = true,
            fogColor = new Color(0.09f, 0.11f, 0.18f),
            fogDensity = 0.02f,
            postExposure = 0.3f,
            contrast = 15f,
            saturation = -5f,
            bloomIntensity = 1.1f,
            bloomThreshold = 0.8f,
            bloomTint = new Color(1f, 0.9f, 0.75f),
            vignette = 0.4f,
            filmGrain = 0.2f,
            chromatic = 0.1f,
        },

        // Victory - warm, resolved, the timeline healed.
        new Mood
        {
            scenePath = "Assets/Scenes/Victory.unity",
            profileName = "Post_Victory",
            ambientSky     = new Color(0.38f, 0.34f, 0.28f),
            ambientEquator = new Color(0.28f, 0.25f, 0.21f),
            ambientGround  = new Color(0.18f, 0.15f, 0.12f),
            fog = true,
            fogColor = new Color(0.32f, 0.28f, 0.24f),
            fogDensity = 0.015f,
            postExposure = 0.45f,
            contrast = 10f,
            saturation = 6f,
            bloomIntensity = 1.3f,
            bloomThreshold = 0.8f,
            bloomTint = new Color(1f, 0.88f, 0.7f),
            vignette = 0.3f,
            filmGrain = 0.12f,
            chromatic = 0.05f,
        },
    };

    // ------------------------------------------------------------------

    private static void Build()
    {
        TuneRenderPipelineAssets();

        for (int i = 0; i < Moods.Length; i++)
        {
            ApplyMood(Moods[i]);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("=== CINEMATIC LOOK COMPLETE ===");
    }

    /// <summary>
    /// Bloom and ACES need highlight range to work on; LDR grading and no MSAA
    /// throw that away and leave hard aliased edges on every railing and step.
    /// </summary>
    private static void TuneRenderPipelineAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null) { continue; }

            // Public setters rather than SerializedProperty: several of these
            // are stored as ENUMS whose index is not the value. m_MSAA is
            // [Disabled,_2x,_4x,_8x], so writing the literal 4 sets an
            // out-of-range index and silently leaves MSAA off - which is
            // exactly what the first version of this pass did. The property
            // setter does the value -> index mapping.
            asset.supportsHDR = true;
            asset.msaaSampleCount = 4;
            asset.colorGradingMode = ColorGradingMode.HighDynamicRange;
            asset.colorGradingLutSize = 32;

            asset.mainLightShadowmapResolution = 2048;
            asset.maxAdditionalLightsCount = 8;
            asset.additionalLightsShadowmapResolution = 2048;
            asset.shadowDistance = 90f;
            asset.shadowCascadeCount = 4;

            // These have public getters but non-public setters, so they can
            // only be reached through serialisation. All are plain booleans,
            // which is safe - unlike the enum-backed fields above, where an
            // index and a value are not the same number.
            var so = new SerializedObject(asset);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_ReflectionProbeBlending", true);
            SetBool(so, "m_ReflectionProbeBoxProjection", true);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);

            Debug.Log("LOOK: tuned RP asset " + asset.name +
                      " (MSAA=" + asset.msaaSampleCount +
                      ", grading=" + asset.colorGradingMode + ")");
        }
    }

    private static void SetBool(SerializedObject so, string path, bool value)
    {
        SerializedProperty p = so.FindProperty(path);

        if (p != null && p.propertyType == SerializedPropertyType.Boolean)
        {
            p.boolValue = value;
        }
    }

    // ------------------------------------------------------------------

    private static void ApplyMood(Mood mood)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(mood.scenePath) == null)
        {
            Debug.LogWarning("LOOK: no scene at " + mood.scenePath + ", skipped.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(mood.scenePath, OpenSceneMode.Single);

        VolumeProfile profile = BuildProfile(mood);
        ApplyEnvironment(mood);
        ApplyPostVolume(profile);
        EnablePostProcessingOnCameras();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("LOOK OK: " + scene.name);
    }

    /// <summary>
    /// Ambient, fog and reflections. Trilight rather than Flat: a single flat
    /// ambient colour makes every surface read the same regardless of which
    /// way it faces, which is a large part of why the greybox looked like
    /// untextured cardboard.
    /// </summary>
    private static void ApplyEnvironment(Mood mood)
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = mood.ambientSky;
        RenderSettings.ambientEquatorColor = mood.ambientEquator;
        RenderSettings.ambientGroundColor = mood.ambientGround;
        RenderSettings.ambientIntensity = 1f;

        RenderSettings.fog = mood.fog;
        RenderSettings.fogColor = mood.fogColor;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = mood.fogDensity;

        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.65f;
    }

    private static VolumeProfile BuildProfile(Mood mood)
    {
        if (!AssetDatabase.IsValidFolder(ProfileFolder))
        {
            AssetDatabase.CreateFolder("Assets/Settings", "Post");
        }

        string path = ProfileFolder + "/" + mood.profileName + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        // ---- Tonemapping: the single most important one. Without ACES the
        // ---- HDR values from bloom and emissives clip to flat white.
        var tone = GetOrAdd<Tonemapping>(profile);
        tone.active = true;
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.ACES;

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.active = true;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = mood.bloomIntensity;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = mood.bloomThreshold;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.72f;
        bloom.tint.overrideState = true;
        bloom.tint.value = mood.bloomTint;
        bloom.highQualityFiltering.overrideState = true;
        bloom.highQualityFiltering.value = true;

        // Base grade. EraColorGrading lives on a SEPARATE, higher-priority
        // volume and only overrides colorFilter/hueShift, so the two compose
        // instead of fighting: this sets exposure/contrast/saturation, the era
        // volume tints on top.
        var grade = GetOrAdd<ColorAdjustments>(profile);
        grade.active = true;
        grade.postExposure.overrideState = true;
        grade.postExposure.value = mood.postExposure;
        grade.contrast.overrideState = true;
        grade.contrast.value = mood.contrast;
        grade.saturation.overrideState = true;
        grade.saturation.value = mood.saturation;

        var vignette = GetOrAdd<Vignette>(profile);
        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = mood.vignette;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.45f;

        var grain = GetOrAdd<FilmGrain>(profile);
        grain.active = true;
        grain.type.overrideState = true;
        grain.type.value = FilmGrainLookup.Medium1;
        grain.intensity.overrideState = true;
        grain.intensity.value = mood.filmGrain;

        var ca = GetOrAdd<ChromaticAberration>(profile);
        ca.active = true;
        ca.intensity.overrideState = true;
        ca.intensity.value = mood.chromatic;

        // A gentle S-curve in the shadows keeps blacks from crushing to a
        // single flat value now that the ambient is a gradient.
        var shadows = GetOrAdd<ShadowsMidtonesHighlights>(profile);
        shadows.active = true;
        shadows.shadows.overrideState = true;
        shadows.shadows.value = new Vector4(1.02f, 1.0f, 1.08f, 0f);
        shadows.highlights.overrideState = true;
        shadows.highlights.value = new Vector4(1.05f, 1.0f, 0.96f, 0f);

        EditorUtility.SetDirty(profile);
        return profile;
    }

    /// <summary>
    /// VolumeProfile.Add creates the component in memory only - it does NOT
    /// add it to the profile's asset file. Without AddObjectToAsset the
    /// components vanish on the next domain reload and the profile reloads
    /// empty, so every effect configured here silently stopped existing the
    /// moment Unity recompiled.
    /// </summary>
    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component;
        if (profile.TryGet(out component))
        {
            return component;
        }

        component = profile.Add<T>(true);
        component.name = typeof(T).Name;

        if (AssetDatabase.Contains(profile))
        {
            component.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        return component;
    }

    /// <summary>
    /// One global volume named PostProcessing, at priority 0 so the
    /// era-grading volume (priority 10) still wins for the params it owns.
    /// </summary>
    private static void ApplyPostVolume(VolumeProfile profile)
    {
        GameObject go = GameObject.Find("PostProcessing");
        if (go == null)
        {
            go = new GameObject("PostProcessing");
        }

        Volume volume = go.GetComponent<Volume>();
        if (volume == null)
        {
            volume = go.AddComponent<Volume>();
        }

        volume.isGlobal = true;
        volume.priority = 0f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }

    /// <summary>
    /// The fix for the flat render: every gameplay camera needs
    /// UniversalAdditionalCameraData with renderPostProcessing on. The
    /// minimap camera is deliberately excluded - post-processing on a
    /// top-down orientation aid costs fill rate and buys nothing.
    /// </summary>
    private static void EnablePostProcessingOnCameras()
    {
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);

        foreach (Camera cam in cameras)
        {
            bool isMinimap = cam.name.ToLowerInvariant().Contains("minimap");

            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
            {
                data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            data.renderPostProcessing = !isMinimap;
            data.antialiasing = isMinimap
                ? AntialiasingMode.None
                : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = !isMinimap;

            if (!isMinimap)
            {
                cam.allowHDR = true;
            }

            EditorUtility.SetDirty(cam);
        }
    }
}
