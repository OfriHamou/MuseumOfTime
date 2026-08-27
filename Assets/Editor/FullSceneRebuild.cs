using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FullSceneRebuild
{
    public static void BuildAll()
    {
        Debug.Log("=== STARTING FULL SCENE REBUILD ===");

        // Import settings BEFORE any prefab is built from those models: the
        // fracture and LOD prefabs copy the imported nodes' transforms, so the
        // importer has to be settled first.
        ModelScaleFixBuilder.BuildFromCommandLine();

        // Shared prefabs/systems first
        AssetPrefabBuilder.BuildFromCommandLine();
        PlayerPrefabBuilder.BuildFromCommandLine();

        // MuseumNight
        MuseumBuilder.BuildFromCommandLine();
        HingeSetBuilder.BuildFromCommandLine();
        WorldPropsPlacer.PlaceFromCommandLine();

        EnsureMuseumPlayer();

        MuseumSceneSetup.BuildCameraRigFromCommandLine();

        // Interior dressing BEFORE the navmesh bake: the display cases and
        // benches have colliders, and agents would path straight through
        // anything added after the surfaces were baked.
        MuseumDressingBuilder.BuildFromCommandLine();

        NavigationBuilder.BuildFromCommandLine();
        CoreSystemsBuilder.BuildFromCommandLine();
        SceneConnectionsBuilder.BuildFromCommandLine();
        TutorialTextBuilder.BuildFromCommandLine();
        HudBuilder.BuildFromCommandLine();
        MinimapBuilder.BuildFromCommandLine();

        // FrozenCity
        TerrainBuilder.BuildFromCommandLine();
        FrozenCityContentBuilder.BuildFromCommandLine();

        // ClockCore
        ClockCoreContentBuilder.BuildFromCommandLine();

        // Final shared polish
        AudioAndVfxBuilder.BuildFromCommandLine();
        ScenePolishBuilder.BuildFromCommandLine();

        // Player presentation
        NoaIntegrationBuilder.BuildFromCommandLine();
        ThirdPersonCameraFixBuilder.BuildFromCommandLine();

        // Per-scene requirement coverage for scenes 2 and 3 (T2/T3/T5).
        SceneGuidanceBuilder.BuildFromCommandLine();

        // The physical hazards carrying collisions 3 and 4 (T4). Must run
        // AFTER SceneGuidanceBuilder, which is what creates the ClockCore
        // pendulums this attaches SwingingHazard to.
        HazardCollisionBuilder.BuildFromCommandLine();

        // ---- Look development -------------------------------------------
        //
        // Order matters here and is not arbitrary:
        //
        //   1. CameraRigParityBuilder must run before the HUD passes, because
        //      it is what gives FrozenCity/ClockCore their second camera.
        //   2. PremiumHudBuilder must run AFTER HudBuilder - it re-skins what
        //      HudBuilder builds and deletes the flat originals.
        //   3. SurfaceDensityBuilder must run after every scene's geometry
        //      exists, since it measures renderers to pick tiling.
        //   4. CinematicLookBuilder must run LAST. It owns ambient, fog and
        //      post-processing, and several earlier builders touch lighting;
        //      running it first means they overwrite it.
        CameraRigParityBuilder.BuildFromCommandLine();
        CharacterLookBuilder.BuildFromCommandLine();

        // Pickups need to be findable before anything else about them matters.
        CollectibleLookBuilder.BuildFromCommandLine();
        SurfaceAndVfxLookBuilder.BuildFromCommandLine();
        SurfaceDensityBuilder.BuildFromCommandLine();
        PremiumHudBuilder.BuildFromCommandLine();
        PremiumMenuBuilder.BuildFromCommandLine();
        CinematicLookBuilder.BuildFromCommandLine();

        // The minimap's map plates are generated FROM the museum geometry, so
        // this has to run after the museum exists. Without it the Minimap
        // layer holds only the player marker and the map is blank (T18).
        MinimapGeometryBuilder.BuildFromCommandLine();

        // Import-side size caps for the 300 MB deliverable (S1).
        BuildSizeBuilder.BuildFromCommandLine();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== FULL SCENE REBUILD COMPLETE ===");
    }

    private static void EnsureMuseumPlayer()
    {
        const string scenePath = "Assets/Scenes/MuseumNight.unity";

        Scene scene = EditorSceneManager.OpenScene(
            scenePath,
            OpenSceneMode.Single);

        GameObject existingPlayer = GameObject.Find("Player");

        if (existingPlayer != null)
        {
            Debug.Log("MUSEUM PLAYER OK: Player already exists.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            PlayerPrefabBuilder.PrefabPath);

        if (prefab == null)
        {
            Debug.LogError(
                "MUSEUM PLAYER FAILED: Could not find Player prefab at " +
                PlayerPrefabBuilder.PrefabPath);

            return;
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(
            prefab,
            scene);

        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.transform.rotation = Quaternion.identity;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("MUSEUM PLAYER OK: Player prefab added to MuseumNight.");
    }
}