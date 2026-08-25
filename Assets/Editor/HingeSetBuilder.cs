using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the museum's hinge-joint set pieces. The requirement is physical
/// hinge joints; three are built so that one misbehaving does not cost the
/// requirement, and each is something the story wanted anyway.
///
///   1. The Clock of Creation pendulum - the GDD's opening image, and
///      literally a hinge. It swings on a spring until time breaks.
///   2. The gallery gate - a motorised hinged gate with angle limits.
///   3. The exhibit signboard - a light hanging sign that swings when the
///      Chrono Orb hits it, which is the cheapest possible proof that the
///      joint reacts to physical impact.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod HingeSetBuilder.BuildFromCommandLine
///
/// Idempotent: the Hinges root is rebuilt each run.
/// </summary>
public static class HingeSetBuilder
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";

    [MenuItem("Museum of Time/Build Hinge Set Pieces")]
    public static void BuildMenu()
    {
        Build();
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("Hinges");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var root = new GameObject("Hinges");

        GameObject props = GameObject.Find("Props");
        if (props != null)
        {
            root.transform.SetParent(props.transform, false);
        }

        Material brass = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumBrass.mat");
        Material wood = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumWood.mat");

        BuildPendulum(root.transform, brass);
        BuildGate(root.transform, wood);
        BuildSignboard(root.transform, wood);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("HINGES OK: pendulum, gate and signboard built with HingeJoints.");
    }

    /// <summary>
    /// The Clock of Creation pendulum. A spring drives it back towards centre
    /// so it keeps a believable resting swing instead of hanging dead still,
    /// and limits stop it from spinning right over the top.
    /// </summary>
    private static void BuildPendulum(Transform root, Material material)
    {
        var pendulum = new GameObject("ClockOfCreationPendulum");
        pendulum.transform.SetParent(root, false);
        pendulum.transform.localPosition = new Vector3(-9f, 4.2f, 8f);

        // The rod hangs below the pivot, so the centre of mass is low and it
        // swings like a real pendulum.
        GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rod.name = "Rod";
        rod.transform.SetParent(pendulum.transform, false);
        rod.transform.localPosition = new Vector3(0f, -1.2f, 0f);
        rod.transform.localScale = new Vector3(0.08f, 2.4f, 0.08f);
        rod.GetComponent<MeshRenderer>().sharedMaterial = material;

        GameObject bob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bob.name = "Bob";
        bob.transform.SetParent(pendulum.transform, false);
        bob.transform.localPosition = new Vector3(0f, -2.4f, 0f);
        bob.transform.localScale = new Vector3(0.6f, 0.06f, 0.6f);
        bob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bob.GetComponent<MeshRenderer>().sharedMaterial = material;

        var body = pendulum.AddComponent<Rigidbody>();
        body.mass = 12f;
        body.angularDamping = 0.05f;
        body.useGravity = true;

        HingeJoint hinge = pendulum.AddComponent<HingeJoint>();
        hinge.anchor = Vector3.zero;
        hinge.axis = new Vector3(0f, 0f, 1f);   // swings in the XY plane
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -35f, max = 35f };

        hinge.useSpring = true;
        hinge.spring = new JointSpring
        {
            spring = 30f,
            damper = 1f,
            targetPosition = 0f,
        };

        // Start it off-centre so it is already swinging when the game begins.
        pendulum.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
    }

    /// <summary>
    /// A motorised gallery gate. The motor drives it open; the limits stop it
    /// tearing through the wall.
    /// </summary>
    private static void BuildGate(Transform root, Material material)
    {
        var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gate.name = "GalleryGate";
        gate.transform.SetParent(root, false);
        // Clear of ClockChamberWest (x = -4, z 2..10): an intersecting
        // gate is jammed against static geometry and cannot move at all.
        // Lifted 0.15m: the slab's top face is at y=0, and a gate resting
        // exactly on it grinds against the floor instead of swinging.
        gate.transform.localPosition = new Vector3(2.5f, 1.25f, -2f);
        gate.transform.localScale = new Vector3(2.6f, 2.1f, 0.12f);
        gate.GetComponent<MeshRenderer>().sharedMaterial = material;

        var body = gate.AddComponent<Rigidbody>();
        body.mass = 20f;
        body.useGravity = false;   // hung on its pins, not resting on the floor

        HingeJoint hinge = gate.AddComponent<HingeJoint>();

        // The anchor sits on the gate's own left edge, which is where the
        // hinge pins would be. In local space that is x = -0.5.
        hinge.anchor = new Vector3(-0.5f, 0f, 0f);
        hinge.axis = new Vector3(0f, 1f, 0f);   // swings about vertical

        hinge.useLimits = true;
        // Symmetric: with min = 0 the joint starts already pinned against
        // its own lower limit and the motor has nowhere to drive it.
        hinge.limits = new JointLimits { min = -95f, max = 95f };

        hinge.useMotor = true;
        hinge.motor = new JointMotor
        {
            targetVelocity = 45f,
            force = 600f,
            freeSpin = false,
        };
    }

    /// <summary>
    /// A hanging exhibit sign. Light and freely swinging, so any physical
    /// impact visibly moves it.
    /// </summary>
    private static void BuildSignboard(Transform root, Material material)
    {
        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "ExhibitSignboard";
        sign.transform.SetParent(root, false);
        sign.transform.localPosition = new Vector3(4f, 2.6f, 6f);
        sign.transform.localScale = new Vector3(1.6f, 0.5f, 0.05f);
        sign.GetComponent<MeshRenderer>().sharedMaterial = material;

        var body = sign.AddComponent<Rigidbody>();
        body.mass = 3f;
        body.angularDamping = 0.1f;

        HingeJoint hinge = sign.AddComponent<HingeJoint>();
        hinge.anchor = new Vector3(0f, 0.5f, 0f);   // hangs from its top edge
        hinge.axis = new Vector3(1f, 0f, 0f);
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -60f, max = 60f };

        // Nudged off vertical so it starts swinging and settles naturally.
        sign.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
    }
}
