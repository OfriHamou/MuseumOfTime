using TMPro;
using UnityEngine;

/// <summary>
/// Makes a pickup look like a pickup: it floats, turns, glows, and says what
/// it is and which key takes it.
///
/// Every collectible in the game was a 0.4 x 0.4 x 0.1 untextured plate on the
/// Default layer with no light and no label, placed in a deliberately dim night
/// museum. The interaction itself worked - a raycast from the camera, E to take
/// it - but there was nothing to tell the player the object existed, that it
/// was the Time Lens, or that E was the key. "I don't know what the Time Lens
/// is or how to pick it up. It does not pick itself up" is the correct reaction
/// to that.
///
/// The label is world space and fades with distance, so it reads as part of the
/// exhibit rather than as HUD clutter.
/// </summary>
public sealed class PickupBeacon : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private TMP_Text label;

    [Tooltip("Metres of vertical bob.")]
    [SerializeField] private float bobHeight = 0.12f;

    [SerializeField] private float bobSpeed = 1.6f;
    [SerializeField] private float spinDegreesPerSecond = 45f;

    [Tooltip("Metres beyond which the label is not drawn.")]
    [SerializeField] private float labelDistance = 12f;

    [Tooltip("Apparent label size. Multiplied by distance to hold it constant.")]
    [SerializeField] private float labelScreenScale = 0.05f;

    private IInteractable interactable;
    private Transform player;
    private Vector3 visualHome;

    private void Awake()
    {
        interactable = GetComponent<IInteractable>();

        if (visual != null)
        {
            visualHome = visual.localPosition;
        }
    }

    private void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");

        if (found != null)
        {
            player = found.transform;
        }
    }

    private void Update()
    {
        if (visual != null)
        {
            // Unscaled, so a pickup does not crawl while the Chrono Hourglass
            // is held - the idle animation is presentation, not gameplay.
            float t = Time.unscaledTime;

            visual.localPosition =
                visualHome + new Vector3(0f, Mathf.Sin(t * bobSpeed) * bobHeight, 0f);

            visual.Rotate(Vector3.up, spinDegreesPerSecond * Time.unscaledDeltaTime, Space.Self);
        }

        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (label == null || player == null)
        {
            return;
        }

        Camera cam = Camera.main;

        if (cam == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > labelDistance)
        {
            label.alpha = 0f;
            return;
        }

        label.alpha = Mathf.Clamp01(1f - (distance / labelDistance)) * 0.6f + 0.4f;

        // Constant apparent size, the same trick the enemy nameplates use.
        float toCamera = Vector3.Distance(cam.transform.position, label.transform.position);
        float size = Mathf.Max(1.5f, toCamera) * labelScreenScale;

        // localScale is not enough on its own. The label hangs off the pickup,
        // and the pickups carry their own non-uniform scale - the Chrono
        // Hourglass is (0.3, 0.5, 0.3) - which multiplies straight through and
        // rendered every prompt squashed to a third of its width. Divide the
        // parent's contribution back out so the text is the shape it was set
        // in, whatever it happens to be hanging from.
        Vector3 parentScale = label.transform.parent != null
            ? label.transform.parent.lossyScale
            : Vector3.one;

        label.transform.localScale = new Vector3(
            size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));

        label.transform.rotation = Quaternion.LookRotation(
            label.transform.position - cam.transform.position);

        string prompt = interactable != null ? interactable.Prompt : "";

        // The key is the part that was missing. Naming the object without
        // naming the key leaves the player standing in front of it.
        label.text = prompt + "\n<size=70%><color=#FFD98A>[ E ]</color></size>";
    }
}
