using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives the two AI agent types real bodies.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod CharacterLookBuilder.BuildFromCommandLine
///
/// Both enemies were primitive Capsules with a flat colour. The Warden even
/// carried a fully built Animator and WardenController (Patrol/Alert/Chase/
/// Attack/Frozen) that had nothing to drive - no avatar, no skinned mesh, and
/// no motion on any state - so the hand-authored controller the brief asks for
/// (T14) was invisible in play.
///
/// This reuses the one imported humanoid rather than adding new art, so the
/// build-size budget (S1) is unaffected: the same mesh and clips, with
/// per-role materials and scales.
///
/// Idempotent - the "Body" child is rebuilt each run.
/// </summary>
public static class CharacterLookBuilder
{
    private const string ModelPath = "Assets/Art/Characters/Noa/Model/Idle.fbx";
    private const string AnimDir = "Assets/Art/Characters/Noa/Animations/";
    private const string WardenControllerPath = "Assets/Animations/Enemies/WardenController.controller";
    private const string MaterialDir = "Assets/Materials/Dressing";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MuseumNight.unity",
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
    };

    [MenuItem("Museum of Time/Build Character Look (enemies)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Material wardenMat = BuildBodyMaterial("WardenBody", new Color(0.20f, 0.24f, 0.34f), 0.28f, false);
        Material shadowMat = BuildBodyMaterial("ShadowBody", new Color(0.10f, 0.09f, 0.16f), 0.35f, true);

        WireWardenController();

        foreach (string scenePath in ScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) { continue; }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int wardens = 0;
            int shadows = 0;

            foreach (WardenAI w in Object.FindObjectsByType<WardenAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                DressAgent(w.gameObject, wardenMat, 1.85f, true);
                wardens++;
            }

            foreach (ShadowAI s in Object.FindObjectsByType<ShadowAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                DressAgent(s.gameObject, shadowMat, 1.55f, false);
                shadows++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("CHARACTER OK: " + scene.name +
                      " (" + wardens + " warden, " + shadows + " shadow)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("=== CHARACTER LOOK COMPLETE ===");
    }

    // ------------------------------------------------------------------

    private static Material BuildBodyMaterial(string name, Color tint, float smoothness, bool ghostly)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }

        // Reuse the character's own albedo so the silhouette still reads as
        // clothing and skin, then push it to the role's colour.
        var albedo = AssetDatabase.LoadAssetAtPath<Texture>(
            "Assets/Art/Characters/Noa/Textures/Ch02_1001_Diffuse.png");

        if (albedo != null && mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", albedo);
        }

        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);

        if (ghostly)
        {
            // The Shadow is a thing made of missing time: translucent, and
            // lit faintly from inside so it stays visible in a dark scene.
            SetTransparent(mat, 0.72f);

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");

                // Kept well under 1. At 0.18/0.10/0.42 x 1.6 the emission was
                // brighter than anything else in a night museum, and with
                // bloom on top the Shadow rendered as a flat neon-violet
                // cutout - a silhouette pasted over the scene rather than a
                // figure standing in it.
                mat.SetColor("_EmissionColor", new Color(0.10f, 0.06f, 0.26f));
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SetTransparent(Material mat, float alpha)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Color c = mat.GetColor("_BaseColor");
        c.a = alpha;
        mat.SetColor("_BaseColor", c);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Puts a skinned humanoid on an agent, hides the placeholder primitive
    /// and makes sure the Animator has an avatar to drive it with.
    /// </summary>
    private static void DressAgent(GameObject agent, Material bodyMaterial,
                                   float targetHeight, bool isWarden)
    {
        // The placeholder capsule is disabled rather than deleted: the
        // MeshFilter/MeshRenderer pair is what several scene builders look for
        // when they re-find these objects.
        var placeholder = agent.GetComponent<MeshRenderer>();
        if (placeholder != null) { placeholder.enabled = false; }

        Transform existing = agent.transform.Find("Body");
        if (existing != null) { Object.DestroyImmediate(existing.gameObject); }

        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelPrefab == null)
        {
            Debug.LogError("CHARACTER: no model at " + ModelPath);
            return;
        }

        var body = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, agent.transform);
        body.name = "Body";
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;

        // One Animator only, on the agent root where the AI drivers expect it.
        Animator childAnimator = body.GetComponent<Animator>();
        if (childAnimator != null) { Object.DestroyImmediate(childAnimator); }

        foreach (SkinnedMeshRenderer smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) { mats[i] = bodyMaterial; }
            smr.sharedMaterials = mats;

            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            smr.receiveShadows = true;
        }

        FitToAgent(body, agent.transform, targetHeight);

        // ---- Animator ------------------------------------------------------
        Animator animator = agent.GetComponent<Animator>();
        if (animator == null) { animator = agent.AddComponent<Animator>(); }

        animator.avatar = LoadAvatar();
        animator.applyRootMotion = false;   // the NavMeshAgent owns movement

        if (animator.runtimeAnimatorController == null)
        {
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(WardenControllerPath);
        }

        if (isWarden)
        {
            var driver = agent.GetComponent<WardenAnimatorDriver>();
            if (driver == null) { driver = agent.AddComponent<WardenAnimatorDriver>(); }

            var so = new SerializedObject(driver);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            var driver = agent.GetComponent<ShadowAnimatorDriver>();
            if (driver == null) { driver = agent.AddComponent<ShadowAnimatorDriver>(); }

            var so = new SerializedObject(driver);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        BuildNameplate(agent, targetHeight);

        EditorUtility.SetDirty(agent);
    }

    /// <summary>
    /// A world-space label naming the agent and its current behaviour.
    ///
    /// Without one, a Chronological Shadow is an unexplained translucent
    /// figure that drifts at you, passes through you and never attacks -
    /// which reads as a broken enemy rather than as a thief you are supposed
    /// to shoot with the Orb.
    /// </summary>
    private static void BuildNameplate(GameObject agent, float targetHeight)
    {
        Transform existing = agent.transform.Find("Nameplate");
        if (existing != null) { Object.DestroyImmediate(existing.gameObject); }

        var go = new GameObject("Nameplate");
        go.transform.SetParent(agent.transform, false);
        go.transform.localPosition = new Vector3(0f, targetHeight + 0.5f, 0f);

        // Scale is driven per-frame by EnemyNameplate so the label holds a
        // constant on-screen size; this is just a sane starting value.
        go.transform.localScale = Vector3.one;

        var text = go.AddComponent<TextMeshPro>();
        text.text = "";

        // World-space TMP measures fontSize in WORLD UNITS, not points, so 2.1
        // meant roughly two metres of text per line and filled the screen
        // whenever an agent came close. A font size of 1 with the transform
        // scaled by distance (EnemyNameplate) keeps it the same apparent size
        // at any range.
        text.fontSize = 1f;
        text.alignment = TextAlignmentOptions.Center;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = Color.white;

        var rect = go.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(4f, 0.7f);
        }

        var plate = go.AddComponent<EnemyNameplate>();
        var so = new SerializedObject(plate);

        SerializedProperty labelProperty = so.FindProperty("label");
        if (labelProperty != null) { labelProperty.objectReferenceValue = text; }

        // Clearance above the head, not a height above the agent origin.
        SerializedProperty heightProperty = so.FindProperty("height");
        if (heightProperty != null) { heightProperty.floatValue = 0.35f; }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Scales the body to the agent's intended height and stands it on the
    /// agent's origin, measured in the AGENT's space - the same correction
    /// the player model needed (see NoaIntegrationBuilder.FitToHuman).
    ///
    /// The Shadow's own transform is scaled to 0.6, so measuring in world
    /// space here would compound that scale into the fit and produce a body
    /// well under a metre tall.
    /// </summary>
    private static void FitToAgent(GameObject body, Transform frame, float targetHeight)
    {
        if (!TryBounds(body, frame, out Bounds raw) || raw.size.y < 0.01f) { return; }

        // frame.lossyScale undoes the agent's own scale, so targetHeight is a
        // real-world metre value regardless of what the agent is scaled to.
        float parentScale = Mathf.Max(0.0001f, frame.lossyScale.y);
        float scale = (targetHeight / parentScale) / raw.size.y;

        body.transform.localScale = Vector3.one * scale;

        if (!TryBounds(body, frame, out Bounds scaled))
        {
            return;
        }

        // A NavMeshAgent does not sit ON the navmesh - it floats its transform
        // baseOffset above it (scaled by the agent's own scale). Grounding the
        // body on the agent's ORIGIN therefore left both characters hovering:
        // baseOffset is 1 on both, so the Warden's feet were a full metre above
        // the floor and the Shadow's 0.6 m.
        //
        // baseOffset is expressed in the agent's own units, which is exactly
        // the space these bounds are measured in, so it subtracts directly.
        float baseOffset = 0f;
        var navAgent = frame.GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (navAgent != null)
        {
            baseOffset = navAgent.baseOffset;
        }

        body.transform.localPosition = new Vector3(0f, -scaled.min.y - baseOffset, 0f);
    }

    private static bool TryBounds(GameObject inst, Transform frame, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;

        foreach (SkinnedMeshRenderer smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) { continue; }

            Bounds local = smr.sharedMesh.bounds;
            Matrix4x4 m = frame.worldToLocalMatrix * smr.transform.localToWorldMatrix;

            Vector3 c = m.MultiplyPoint3x4(local.center);
            Vector3 e = local.extents;
            var worldExtents = new Vector3(
                Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);

            var b = new Bounds(c, worldExtents * 2f);

            if (!any) { bounds = b; any = true; }
            else { bounds.Encapsulate(b); }
        }

        return any;
    }

    private static Avatar LoadAvatar()
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            var avatar = o as Avatar;
            if (avatar != null && avatar.isValid) { return avatar; }
        }

        return null;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// The hand-authored WardenController had five states and no motion on
    /// any of them, so every state played nothing. The structure, parameters
    /// and transitions are left exactly as authored - only the Motion slots
    /// are filled, which is what T14 asks for ("an Animator you defined").
    /// </summary>
    private static void WireWardenController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WardenControllerPath);
        if (controller == null || controller.layers.Length == 0) { return; }

        var byState = new Dictionary<string, AnimationClip>
        {
            { "Patrol", FindClip(AnimDir + "Walking.fbx") },
            { "Alert",  FindClip(ModelPath) },
            { "Chase",  FindClip(AnimDir + "Running.fbx") },
            { "Attack", FindClip(AnimDir + "Throw Object.fbx") },
            { "Frozen", FindClip(ModelPath) },
        };

        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            if (byState.TryGetValue(child.state.name, out AnimationClip clip) && clip != null)
            {
                child.state.motion = clip;
            }
        }

        EditorUtility.SetDirty(controller);
        Debug.Log("CHARACTER: WardenController motions wired.");
    }

    private static AnimationClip FindClip(string fbxPath)
    {
        AnimationClip fallback = null;

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            var clip = o as AnimationClip;

            // Skip Unity's hidden __preview__ clips.
            if (clip != null && !clip.name.StartsWith("__"))
            {
                if (fallback == null) { fallback = clip; }
            }
        }

        return fallback;
    }
}
