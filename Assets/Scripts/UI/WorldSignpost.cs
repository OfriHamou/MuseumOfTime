using UnityEngine;

/// <summary>
/// A world-space sign that turns to face the player and nothing else.
///
/// This exists because the two components that already billboard world text
/// both also REWRITE it: WorldObjectiveText replaces the text with the current
/// objective line, and WorldTutorialText swaps in progress-aware copy. Reusing
/// either one for a fixed wayfinding sign meant the sign silently displayed
/// the objective instead of the direction it was placed to give - three signs
/// reading "Objective: Reach the Clock of Creation" in letters wide enough to
/// span the screen, which is worse than having no sign at all.
///
/// Billboarding and authoring the text are separate jobs. This does only the
/// first.
/// </summary>
[RequireComponent(typeof(TMPro.TextMeshPro))]
public sealed class WorldSignpost : MonoBehaviour
{
    [Tooltip("Beyond this distance the sign fades out, so a room full of " +
             "signs does not read as clutter from across the hall.")]
    [SerializeField] private float visibleDistance = 26f;

    private TMPro.TextMeshPro label;
    private Transform player;

    private void Awake()
    {
        label = GetComponent<TMPro.TextMeshPro>();
    }

    private void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");

        if (found != null)
        {
            player = found.transform;
        }
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            return;
        }

        // Face away from the camera, which is what makes the text read the
        // right way round rather than mirrored.
        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position);

        if (label == null || player == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        label.alpha = distance > visibleDistance
            ? 0f
            : Mathf.Clamp01(1f - (distance / visibleDistance)) * 0.5f + 0.5f;
    }
}
