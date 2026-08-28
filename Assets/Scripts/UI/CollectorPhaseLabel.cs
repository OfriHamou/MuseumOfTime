using TMPro;
using UnityEngine;

/// <summary>
/// A label floating over the Collector saying which era it can be hurt in,
/// and whether the player is currently in that era.
///
/// The objective banner already says what to do, but it sits at the top of the
/// screen and the player is looking at the boss. After the shield breaks, the
/// fight silently changes the rule - the same orb that just worked now does
/// nothing - and with nothing to explain that, the reasonable thing to do is
/// wander around throwing orbs and waiting for something to happen. Reported
/// exactly that way: "after I break the thing, it's not clear what I need to
/// do, I find myself going in loops".
///
/// So the rule lives on the boss. It is red while the player is in the wrong
/// era and green while they are in the right one, which turns "why is nothing
/// happening" into a thing you can see and act on.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public sealed class CollectorPhaseLabel : MonoBehaviour
{
    [SerializeField] private Collector collector;

    [Tooltip("Height above the Collector's own origin.")]
    [SerializeField] private float height = 2.6f;

    private TextMeshPro label;

    private void Awake()
    {
        label = GetComponent<TextMeshPro>();

        if (collector == null)
        {
            collector = GetComponentInParent<Collector>();
        }
    }

    private void LateUpdate()
    {
        if (label == null || collector == null)
        {
            return;
        }

        Camera cam = Camera.main;

        if (cam != null)
        {
            transform.position = collector.transform.position + (Vector3.up * height);

            transform.rotation = Quaternion.LookRotation(
                transform.position - cam.transform.position);

            // Constant apparent size, so it stays readable from across the
            // chamber and does not fill the screen up close.
            float toCamera = Vector3.Distance(cam.transform.position, transform.position);
            transform.localScale = Vector3.one * (Mathf.Max(2f, toCamera) * 0.055f);
        }

        label.text = Describe();
    }

    private string Describe()
    {
        if (collector.IsDefeated)
        {
            return "<color=#8BE29A>THE COLLECTOR IS UNDONE</color>";
        }

        // The name and what it is, every frame. It was an unlabelled shape in
        // a room, and "I don't get what the boss is" is the fair reading of an
        // enemy the game never introduces.
        int phase = collector.CurrentStage == Collector.Stage.Shielded ? 1
            : collector.CurrentStage == Collector.Stage.Present ? 2 : 3;

        // Say how far through the fight this is. Without it there is no sense
        // of progress at all - each phase looks like the last one failing.
        string title =
            "<color=#C9B3FF>THE COLLECTOR</color>  <size=60%>PHASE " + phase +
            " OF 3</size>\n" +
            "<size=60%>It is unmaking the timeline. Undo it with the Chrono Orb.</size>\n";

        TimeEra needed;
        string extra;

        switch (collector.CurrentStage)
        {
            case Collector.Stage.Shielded:
                needed = TimeEra.Past;
                extra = "Two orb hits break the shield";
                break;

            case Collector.Stage.Present:
                needed = TimeEra.Present;
                extra = "Hit it with the Orb";
                break;

            default:
                needed = TimeEra.Future;
                extra = "Hold CTRL to slow time, then hit it";
                break;
        }

        TimeEra current = EraManager.Instance != null
            ? EraManager.Instance.CurrentEra
            : TimeEra.Present;

        bool ready = current == needed;

        string key = needed == TimeEra.Past ? "Q"
            : needed == TimeEra.Future ? "R" : "Q / R";

        if (ready)
        {
            return "<color=#8BE29A>VULNERABLE NOW</color>\n<size=70%>" + extra + "</size>";
        }

        return "<color=#FF7A72>IMMUNE - only vulnerable in the " +
               needed.ToString().ToUpperInvariant() + "</color>\n" +
               "<size=70%>Press " + key + " to change era (you are in the " +
               current.ToString().ToUpperInvariant() + ")</size>";
    }
}
