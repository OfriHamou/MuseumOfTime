using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 7: places the audio pipeline (Step 7.1) and the lighting, era color
/// grading and particle effects (Step 7.2) into the three gameplay scenes.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod AudioAndVfxBuilder.BuildFromCommandLine
///
/// Idempotent, like every other builder in this project. Re-run it after
/// creating the AudioMixer asset by hand (see Phase7_Unity_Walkthrough.md)
/// and it will auto-wire the mixer into every scene's AudioManager.
/// </summary>
public static class AudioAndVfxBuilder
{
    private const string OrbPrefabPath = "Assets/Prefabs/World/ChronoOrb.prefab";
    private const string MixerPath = "Assets/Audio/GameAudioMixer.mixer";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MuseumNight.unity",
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
    };

    [MenuItem("Museum of Time/Build Audio and VFX (Phase 7)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        AddOrbTrailToPrefab();

        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);

        foreach (string path in ScenePaths)
        {
            BuildScene(path, mixer);
        }

        Debug.Log("AUDIO AND VFX OK: AudioManager (mixer " +
                   (mixer != null ? "wired" : "not present, using low-pass fallback") +
                   "), era color grading, GameplayVfx, era-switch VFX and Shadow drift in " +
                   ScenePaths.Length + " scenes; MuseumNight lighting; orb trail on the prefab.");
    }

    private static void BuildScene(string path, AudioMixer mixer)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        BuildAudioManager(mixer);
        BuildEraColorGrading();
        BuildEraSwitchVfx();
        BuildGameplayVfx();
        AddShadowDrift();

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "MuseumNight")
        {
            BuildMuseumLighting();
        }
        else if (sceneName == "FrozenCity")
        {
            BuildFrozenCityLighting();
        }
        else if (sceneName == "ClockCore")
        {
            BuildClockCoreLighting();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ---------------- audio ----------------

    private static void BuildAudioManager(AudioMixer mixer)
    {
        GameObject go = FindOrCreate("AudioManager", null);
        AudioManager audio = Ensure<AudioManager>(go);

        var so = new SerializedObject(audio);

        if (mixer != null)
        {
            so.FindProperty("mixer").objectReferenceValue = mixer;
            so.FindProperty("musicGroup").objectReferenceValue = FirstGroup(mixer, "Music");
            so.FindProperty("sfxGroup").objectReferenceValue = FirstGroup(mixer, "SFX");
            so.FindProperty("normalSnapshot").objectReferenceValue = mixer.FindSnapshot("Normal");
            so.FindProperty("slowTimeSnapshot").objectReferenceValue = mixer.FindSnapshot("SlowTime");
        }
        else
        {
            // Clear stale references if the mixer was removed.
            so.FindProperty("mixer").objectReferenceValue = null;
            so.FindProperty("musicGroup").objectReferenceValue = null;
            so.FindProperty("sfxGroup").objectReferenceValue = null;
            so.FindProperty("normalSnapshot").objectReferenceValue = null;
            so.FindProperty("slowTimeSnapshot").objectReferenceValue = null;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AudioMixerGroup FirstGroup(AudioMixer mixer, string name)
    {
        AudioMixerGroup[] groups = mixer.FindMatchingGroups(name);
        return groups != null && groups.Length > 0 ? groups[0] : null;
    }

    // ---------------- era color grading ----------------

    private static void BuildEraColorGrading()
    {
        GameObject go = FindOrCreate("EraGrading", null);

        Volume volume = Ensure<Volume>(go);
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.weight = 1f;

        Ensure<EraColorGrading>(go);
    }

    // ---------------- particle effects ----------------

    private static void BuildEraSwitchVfx()
    {
        GameObject go = FindOrCreate("EraSwitchVfx", null);
        ConfigureBurst(EnsureParticles(go), 40, 0.15f, new Color(0.7f, 0.85f, 1f, 0.9f), 0.3f);
        Ensure<EraSwitchVfx>(go);
    }

    private static void BuildGameplayVfx()
    {
        GameObject go = FindOrCreate("GameplayVfx", null);
        Ensure<GameplayVfx>(go);
    }

    private static void AddShadowDrift()
    {
        foreach (ShadowAI shadow in Object.FindObjectsByType<ShadowAI>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform existing = shadow.transform.Find("ShadowDrift");
            if (existing != null)
            {
                continue;
            }

            var drift = new GameObject("ShadowDrift");
            drift.transform.SetParent(shadow.transform, false);

            ParticleSystem ps = EnsureParticles(drift);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.2f;
            main.startSize = 0.25f;
            main.startColor = new Color(0.35f, 0.3f, 0.5f, 0.5f);
            // World space, so particles linger where the Shadow was - the drift.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 8f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
        }
    }

    private static void AddOrbTrailToPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("Orb prefab not found at " + OrbPrefabPath + " - skipping orb trail.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(OrbPrefabPath);

        if (root.transform.Find("OrbTrail") == null)
        {
            var trail = new GameObject("OrbTrail");
            trail.transform.SetParent(root.transform, false);

            ParticleSystem ps = EnsureParticles(trail);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = 0.12f;
            main.startColor = new Color(0.7f, 0.9f, 1f, 0.7f);
            // World space so the emitted particles stay put as the orb flies on,
            // leaving a trail behind it.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 40f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            PrefabUtility.SaveAsPrefabAsset(root, OrbPrefabPath);
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    private static Material particleMaterial;

    /// <summary>
    /// A ParticleSystemRenderer's default material uses a Built-in-RP shader,
    /// which URP renders as a solid magenta/pink error colour - the "broken
    /// materials" bug every particle effect in the game was silently hitting.
    /// URP ships a dedicated unlit particle shader for exactly this case.
    /// </summary>
    private static Material GetParticleMaterial()
    {
        if (particleMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit");
            particleMaterial = new Material(shader) { name = "VfxParticleUnlit" };
            particleMaterial.SetFloat("_Surface", 1f); // Transparent
            particleMaterial.SetFloat("_Blend", 0f);   // Alpha blend
            particleMaterial.SetOverrideTag("RenderType", "Transparent");
            particleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return particleMaterial;
    }

    private static ParticleSystem EnsureParticles(GameObject go)
    {
        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            ps = go.AddComponent<ParticleSystem>();
        }

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetParticleMaterial();
        return ps;
    }

    private static void ConfigureBurst(ParticleSystem ps, int count, float size, Color color, float radius)
    {
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = 1f;
        main.startSpeed = 3f;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
    }

    // ---------------- MuseumNight lighting ----------------

    private static void BuildMuseumLighting()
    {
        // Deep shadows: a dim, cool ambient floor so the pooled lights read.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.10f);

        GameObject root = FindOrCreate("MuseumLighting", null);
        ClearChildren(root);

        // Cold moonlight from above - the one shadow-casting key light, kept
        // to one so the real-time shadow count stays low (the plan's stated
        // performance risk on the two-storey museum).
        var moon = new GameObject("Moonlight");
        moon.transform.SetParent(root.transform, false);
        moon.transform.rotation = Quaternion.Euler(60f, -20f, 0f);
        Light moonlight = moon.AddComponent<Light>();
        moonlight.type = LightType.Directional;
        moonlight.color = new Color(0.6f, 0.72f, 1f);
        moonlight.intensity = 0.5f;
        moonlight.shadows = LightShadows.Soft;

        // Warm pooled exhibit spots, shadows off (cheap), pointing straight
        // down over the key exhibits.
        Vector3[] exhibits =
        {
            new Vector3(-8f, 6f, 8f),   // the Clock of Creation / its plaque
            new Vector3(4f, 6f, 4f),    // a Time Shard
            new Vector3(-4f, 6f, -4f),  // a Time Shard
            new Vector3(9f, 6f, 6f),    // the curator's office / Time Lens
        };

        for (int i = 0; i < exhibits.Length; i++)
        {
            var spotGo = new GameObject("ExhibitLight_" + i);
            spotGo.transform.SetParent(root.transform, false);
            spotGo.transform.position = exhibits[i];
            spotGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Light spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.82f, 0.55f);
            spot.intensity = 3.5f;
            spot.range = 10f;
            spot.spotAngle = 55f;
            spot.shadows = LightShadows.None;
        }
    }

    /// <summary>
    /// FrozenCity: a cool, low, dusk-blue key light (the city froze before
    /// sunset) with a cold ambient floor, so the warm lanterns along the
    /// central path read as the one point of remaining life/warmth.
    /// </summary>
    private static void BuildFrozenCityLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.13f, 0.18f);

        GameObject root = FindOrCreate("FrozenCityLighting", null);
        ClearChildren(root);

        var sun = new GameObject("DuskLight");
        sun.transform.SetParent(root.transform, false);
        sun.transform.rotation = Quaternion.Euler(15f, -40f, 0f);
        Light dusk = sun.AddComponent<Light>();
        dusk.type = LightType.Directional;
        dusk.color = new Color(0.55f, 0.68f, 0.95f);
        dusk.intensity = 0.6f;
        dusk.shadows = LightShadows.Soft;
    }

    /// <summary>
    /// ClockCore: a moody violet ambient (the arena the museum's own clock
    /// broke into) with one warm, focused spotlight over the Collector's
    /// dais, so the boss reads as the room's one deliberate focal point.
    /// </summary>
    private static void BuildClockCoreLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.09f, 0.06f, 0.14f);

        GameObject root = FindOrCreate("ClockCoreLighting", null);
        ClearChildren(root);

        var rim = new GameObject("TimeRimLight");
        rim.transform.SetParent(root.transform, false);
        rim.transform.rotation = Quaternion.Euler(35f, 130f, 0f);
        Light rimLight = rim.AddComponent<Light>();
        rimLight.type = LightType.Directional;
        rimLight.color = new Color(0.55f, 0.4f, 0.9f);
        rimLight.intensity = 0.4f;
        rimLight.shadows = LightShadows.None;

        var spotGo = new GameObject("CollectorSpotlight");
        spotGo.transform.SetParent(root.transform, false);
        spotGo.transform.position = new Vector3(0f, 7.5f, 8f);
        spotGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Light spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1f, 0.75f, 0.4f);
        spot.intensity = 6f;
        spot.range = 14f;
        spot.spotAngle = 45f;
        spot.shadows = LightShadows.Soft;
    }

    // ---------------- helpers ----------------

    private static GameObject FindOrCreate(string name, GameObject parent)
    {
        GameObject found = GameObject.Find(name);

        if (found == null)
        {
            found = new GameObject(name);

            if (parent != null)
            {
                found.transform.SetParent(parent.transform, false);
            }
        }

        return found;
    }

    private static void ClearChildren(GameObject parent)
    {
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.transform.GetChild(i).gameObject);
        }
    }

    private static T Ensure<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component == null ? target.AddComponent<T>() : component;
    }
}
