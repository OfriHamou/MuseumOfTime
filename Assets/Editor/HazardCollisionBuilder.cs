using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the physical hazards that carry the third and fourth graded
/// collisions (T4).
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod HazardCollisionBuilder.BuildFromCommandLine
///
/// T4 asks for at least three collisions detected and acted upon, and Step 3.3
/// of the plan names four by intent: orb-to-object, falling debris-to-player,
/// Warden-to-player and orb-to-boss-shield. Only TWO existed in code -
/// ChronoOrb and Collector - so the requirement was one short of its own
/// minimum.
///
/// Warden-to-player was quietly dropped rather than written, because Noa moves
/// on a CharacterController and a CharacterController never raises
/// OnCollisionEnter; a "Warden collision" would have had to be a trigger, which
/// is a different requirement (T3). Falling debris and a swinging pendulum both
/// have Rigidbodies, so their OnCollisionEnter fires against Noa's collider for
/// real, and both scale their response from the contact data.
///
/// Idempotent.
/// </summary>
public static class HazardCollisionBuilder
{
    [MenuItem("Museum of Time/Build Hazard Collisions")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        BuildScene("Assets/Scenes/MuseumNight.unity", new[]
        {
            new Vector3(-2.5f, 6.2f, -5f),
            new Vector3(3.5f, 6.2f, 2.5f),
            new Vector3(-7f, 6.2f, 4f),
        });

        BuildScene("Assets/Scenes/FrozenCity.unity", new[]
        {
            new Vector3(4f, 7.5f, 12f),
            new Vector3(-5f, 7.5f, 18f),
        });

        BuildScene("Assets/Scenes/ClockCore.unity", new[]
        {
            new Vector3(-5f, 7.4f, 4f),
            new Vector3(5f, 7.4f, 4f),
        });

        Debug.Log("=== HAZARD COLLISIONS COMPLETE ===");
    }

    private static void BuildScene(string scenePath, Vector3[] debrisPoints)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) { return; }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        GameObject root = GameObject.Find("Hazards");
        if (root == null) { root = new GameObject("Hazards"); }

        Material stone = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumMarble.mat");

        for (int i = 0; i < debrisPoints.Length; i++)
        {
            BuildDebris(root, "FallingDebris_" + i, debrisPoints[i], stone);
        }

        // Any hinged bob in the scene also becomes a hazard.
        int pendulums = 0;
        foreach (HingeJoint hinge in Object.FindObjectsByType<HingeJoint>(FindObjectsInactive.Include))
        {
            // Only the free-swinging bobs, not motorised gates.
            if (hinge.useMotor) { continue; }

            if (hinge.GetComponent<SwingingHazard>() == null)
            {
                hinge.gameObject.AddComponent<SwingingHazard>();
            }

            EditorUtility.SetDirty(hinge.gameObject);
            pendulums++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("HAZARD OK: " + scene.name + " (" + debrisPoints.Length +
                  " debris, " + pendulums + " swinging hazards)");
    }

    private static void BuildDebris(GameObject root, string name, Vector3 position, Material material)
    {
        Transform existing = root.transform.Find(name);
        GameObject go;

        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root.transform, true);
        }

        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(
            Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
        go.transform.localScale = new Vector3(0.7f, 0.35f, 0.6f);

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        var collider = go.GetComponent<BoxCollider>();
        if (collider == null) { collider = go.AddComponent<BoxCollider>(); }
        collider.isTrigger = false;

        var body = go.GetComponent<Rigidbody>();
        if (body == null) { body = go.AddComponent<Rigidbody>(); }
        body.mass = 4f;
        body.isKinematic = true;

        if (go.GetComponent<FallingDebris>() == null)
        {
            go.AddComponent<FallingDebris>();
        }

        EditorUtility.SetDirty(go);
    }
}
