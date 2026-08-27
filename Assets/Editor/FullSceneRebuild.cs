using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FullSceneRebuild
{
    public static void BuildAll()
    {
        Debug.Log("=== STARTING FULL SCENE REBUILD ===");

        // Shared prefabs/systems first
        AssetPrefabBuilder.BuildFromCommandLine();
        PlayerPrefabBuilder.BuildFromCommandLine();

        // MuseumNight
        MuseumBuilder.BuildFromCommandLine();
        HingeSetBuilder.BuildFromCommandLine();
        WorldPropsPlacer.PlaceFromCommandLine();

        EnsureMuseumPlayer();

        MuseumSceneSetup.BuildCameraRigFromCommandLine();

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