using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives the player a visible Noa body using the imported Mixamo model +
/// animations under Assets/Art/Characters/Noa/, without redesigning the
/// player system: the model is a child of the player, the existing root
/// Animator keeps the hand-built NoaController and gains Noa's Humanoid
/// Avatar, and the imported model's own Animator is removed so there is only
/// one. Clip import loop flags are set, and the Mixamo clips are wired into
/// NoaController's existing states (structure/parameters/transitions kept).
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod NoaIntegrationBuilder.BuildFromCommandLine
///
/// Idempotent: the "NoaModel" child is removed and rebuilt each run.
/// </summary>
public static class NoaIntegrationBuilder
{
    private const string ModelPath = "Assets/Art/Characters/Noa/Model/Idle.fbx";
    private const string AnimDir = "Assets/Art/Characters/Noa/Animations/";
    private const string ControllerPath = "Assets/Animations/Player/NoaController.controller";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const string MuseumNightPath = "Assets/Scenes/MuseumNight.unity";

    private const float TargetHeight = 1.75f;

    [MenuItem("Museum of Time/Integrate Noa Character")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        ConfigureClips();

        Avatar avatar = LoadAvatar();
        if (avatar == null)
        {
            Debug.LogError("NOA FAILED: no Humanoid Avatar found in " + ModelPath);
            return;
        }

        WireController();

        UpdatePlayerPrefab(avatar);
        UpdateMuseumNightPlayer(avatar);

        Debug.Log("NOA OK: avatar '" + avatar.name + "' assigned; NoaModel added to Player.prefab and " +
                   "MuseumNight player; NoaController wired to Mixamo clips.");
    }

    // -----------------------------------------------------------------
    // 1. Clip import config: names + loop flags
    // -----------------------------------------------------------------

    private static void ConfigureClips()
    {
        ConfigClip(ModelPath, "Idle", true);
        ConfigClip(AnimDir + "Walking.fbx", "Walking", true);
        ConfigClip(AnimDir + "Running.fbx", "Running", true);
        ConfigClip(AnimDir + "Jump.fbx", "Jump", false);
        ConfigClip(AnimDir + "Throw Object.fbx", "ThrowObject", false);
    }

    private static void ConfigClip(string fbxPath, string clipName, bool loop)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("NOA: no ModelImporter for " + fbxPath);
            return;
        }

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips.Length == 0)
        {
            Debug.LogWarning("NOA: no clips in " + fbxPath);
            return;
        }

        clips[0].name = clipName;
        clips[0].loopTime = loop;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    // -----------------------------------------------------------------
    // 2. Avatar
    // -----------------------------------------------------------------

    private static Avatar LoadAvatar()
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (o is Avatar a && a.isHuman)
            {
                return a;
            }
        }

        return null;
    }

    // -----------------------------------------------------------------
    // 3. Wire NoaController states to the Mixamo clips (structure kept)
    // -----------------------------------------------------------------

    private static void WireController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("NOA FAILED: NoaController not found at " + ControllerPath);
            return;
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        SetMotion(sm, "Idle", LoadClip(ModelPath, "Idle"));
        SetMotion(sm, "Walk", LoadClip(AnimDir + "Walking.fbx", "Walking"));
        SetMotion(sm, "Run", LoadClip(AnimDir + "Running.fbx", "Running"));
        SetMotion(sm, "Jump", LoadClip(AnimDir + "Jump.fbx", "Jump"));
        SetMotion(sm, "Interact", LoadClip(AnimDir + "Throw Object.fbx", "ThrowObject"));

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static void SetMotion(AnimatorStateMachine sm, string stateName, Motion motion)
    {
        if (motion == null)
        {
            Debug.LogWarning("NOA: no clip for state " + stateName);
            return;
        }

        foreach (ChildAnimatorState cs in sm.states)
        {
            if (cs.state.name == stateName)
            {
                cs.state.motion = motion;
                return;
            }
        }

        Debug.LogWarning("NOA: state '" + stateName + "' not found in NoaController.");
    }

    private static AnimationClip LoadClip(string fbxPath, string clipName)
    {
        AnimationClip fallback = null;

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview"))
            {
                if (c.name == clipName)
                {
                    return c;
                }

                fallback = fallback ?? c;
            }
        }

        return fallback;
    }

    // -----------------------------------------------------------------
    // 4. Attach the model + avatar to the players
    // -----------------------------------------------------------------

    private static void UpdatePlayerPrefab(Avatar avatar)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        RemovePlaceholderVisual(root);
        AttachNoaModel(root);
        AssignAvatar(root, avatar);

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void UpdateMuseumNightPlayer(Avatar avatar)
    {
        Scene scene = EditorSceneManager.OpenScene(MuseumNightPath, OpenSceneMode.Single);

        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("NOA: no 'Player' in MuseumNight - skipping.");
            return;
        }

        RemovePlaceholderVisual(player);
        AttachNoaModel(player);
        AssignAvatar(player, avatar);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// Removes any MeshRenderer/MeshFilter sitting directly on the player
    /// root - a leftover placeholder visual (e.g. a primitive Capsule) that
    /// pre-dates the Noa model and would otherwise render alongside it. The
    /// CharacterController and every other component are left untouched.
    /// </summary>
    private static void RemovePlaceholderVisual(GameObject playerRoot)
    {
        var mr = playerRoot.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Object.DestroyImmediate(mr);
        }

        var mf = playerRoot.GetComponent<MeshFilter>();
        if (mf != null)
        {
            Object.DestroyImmediate(mf);
        }
    }

    private static void AttachNoaModel(GameObject playerRoot)
    {
        // Remove EVERY existing character visual, not just the first child
        // that happens to be called "NoaModel".
        //
        // The MuseumNight player is a prefab instance of Player.prefab, and
        // this builder updates both the prefab AND the scene instance. The
        // prefab's own NoaModel therefore already exists on the instance by
        // the time the instance is processed, and Transform.Find returns only
        // the first match - so each run left the old buried model in place and
        // added a second one beside it. Two Noas, one of them a metre
        // underground.
        //
        // Any child carrying a SkinnedMeshRenderer is a character visual; the
        // player root has no other use for one.
        for (int i = playerRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = playerRoot.transform.GetChild(i);

            bool isCharacterVisual =
                child.name == "NoaModel" ||
                child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;

            if (isCharacterVisual)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, playerRoot.transform);
        inst.name = "NoaModel";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        // Remove the imported model's own Animator so there is only one, on
        // the player root.
        Animator childAnim = inst.GetComponent<Animator>();
        if (childAnim != null)
        {
            Object.DestroyImmediate(childAnim);
        }

        FitToHuman(inst);
    }

    private static void AssignAvatar(GameObject playerRoot, Avatar avatar)
    {
        Animator anim = playerRoot.GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogWarning("NOA: player root has no Animator - skipping avatar.");
            return;
        }

        anim.avatar = avatar;
        anim.applyRootMotion = false;   // movement stays script-driven
    }

    /// <summary>
    /// Scales the model to ~1.75 m and puts its feet on the floor.
    ///
    /// The previous version measured the bind pose in WORLD space and then
    /// subtracted that from LOCAL position, which is only equivalent while the
    /// player root sits at world y = 0. It does not: the player spawns at
    /// y = 0.08, and the Mixamo mesh bounds are expressed about the root bone
    /// rather than the model origin, so the two errors compounded into a
    /// localPosition of (0, -1, 0) - Noa rendered buried to the waist, with
    /// her feet 0.95 m below a floor whose top surface is y = 0.
    ///
    /// Measuring in the PLAYER ROOT's own space makes the result independent
    /// of where in the world the player happens to be.
    /// </summary>
    private static void FitToHuman(GameObject inst)
    {
        Transform frame = inst.transform.parent;
        if (frame == null)
        {
            return;
        }

        inst.transform.localPosition = Vector3.zero;
        inst.transform.localScale = Vector3.one;

        if (!TryBindPoseBounds(inst, frame, out Bounds bounds) || bounds.size.y < 0.01f)
        {
            return;
        }

        float scale = TargetHeight / bounds.size.y;
        inst.transform.localScale = Vector3.one * scale;

        if (!TryBindPoseBounds(inst, frame, out Bounds scaled))
        {
            return;
        }

        // A CharacterController rests with its capsule bottom skinWidth above
        // the ground, so dropping the feet exactly to the capsule bottom would
        // leave Noa hovering by that much. Take it off as well.
        float skin = 0f;
        var controller = frame.GetComponent<CharacterController>();
        if (controller != null)
        {
            skin = controller.skinWidth;
        }

        inst.transform.localPosition = new Vector3(0f, -scaled.min.y - skin, 0f);

        Debug.Log("NOA: model fitted - scale " + scale.ToString("F3") +
                  ", localPosition " + inst.transform.localPosition +
                  " (measured in " + frame.name + " space)");
    }

    /// <summary>
    /// Bounds of the model's bind-pose meshes, expressed in <paramref
    /// name="frame"/>'s local space. Uses the shared mesh bounds (not
    /// SkinnedMeshRenderer.bounds) so it is deterministic in edit mode with no
    /// Animator playing.
    /// </summary>
    private static bool TryBindPoseBounds(GameObject inst, Transform frame, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;

        foreach (SkinnedMeshRenderer smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null)
            {
                continue;
            }

            Bounds local = smr.sharedMesh.bounds;
            Matrix4x4 m = frame.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            Vector3 c = m.MultiplyPoint3x4(local.center);
            Vector3 e = local.extents;
            Vector3 worldExtents = new Vector3(
                Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);

            var b = new Bounds(c, worldExtents * 2f);

            if (!any) { bounds = b; any = true; }
            else { bounds.Encapsulate(b); }
        }

        return any;
    }
}
