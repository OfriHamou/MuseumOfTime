using TMPro;
using UnityEngine;

/// <summary>
/// A world-space label over an AI agent saying what it is and what it is
/// doing.
///
/// The two agent types were unreadable in play. A Chronological Shadow is a
/// translucent figure trailing particles that drifts toward you, passes
/// straight through you and never attacks - so the honest player reaction is
/// "what is that, is it hurting me, and what am I supposed to do?". None of
/// that was answerable from anything on screen.
///
/// The label answers all three: the name, the current behaviour, and - while
/// the agent is a live threat - the counter-play. It is deliberately world
/// space and distance-faded rather than a screen overlay, so it reads as part
/// of the scene and does not clutter the HUD.
/// </summary>
public sealed class EnemyNameplate : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Tooltip("Metres beyond which the label is not drawn at all.")]
    [SerializeField] private float visibleDistance = 22f;

    [SerializeField] private float fadeStart = 16f;

    [Tooltip("Clearance above the top of the body, in metres.")]
    [SerializeField] private float height = 0.35f;

    [Tooltip("Apparent size. Multiplied by distance to hold a constant on-screen size.")]
    [SerializeField] private float screenScale = 0.045f;

    private ShadowAI shadow;
    private WardenAI warden;
    private Transform player;

    private void Awake()
    {
        shadow = GetComponentInParent<ShadowAI>();
        warden = GetComponentInParent<WardenAI>();

        if (label == null)
        {
            label = GetComponent<TMP_Text>();
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

    private void LateUpdate()
    {
        if (label == null)
        {
            return;
        }

        Camera cam = Camera.main;

        if (cam == null || player == null)
        {
            return;
        }

        Transform agent = transform.parent != null ? transform.parent : transform;

        // Sit above the BODY, measured from its renderers, rather than a fixed
        // offset from the agent's origin. A NavMeshAgent's origin floats
        // baseOffset above the ground, so a constant offset put the Shadow's
        // label most of a metre above its own head.
        transform.position = new Vector3(
            agent.position.x,
            TopOfBody(agent) + height,
            agent.position.z);

        // Billboard, so it reads from either camera mode.
        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position);

        // Constant apparent size. A fixed world-space size is unreadable at
        // the far end of a 30 m gallery and fills the screen when the agent
        // walks into you - the label has one job, so it should be the same
        // size whenever it is on screen at all.
        float toCamera = Vector3.Distance(cam.transform.position, transform.position);
        float parentScale = agent.lossyScale.y <= 0.0001f ? 1f : agent.lossyScale.y;

        transform.localScale =
            Vector3.one * (Mathf.Max(1.5f, toCamera) * screenScale / parentScale);

        float distance = Vector3.Distance(agent.position, player.position);

        if (distance > visibleDistance)
        {
            label.alpha = 0f;
            return;
        }

        label.alpha = distance <= fadeStart
            ? 1f
            : Mathf.Clamp01(1f - ((distance - fadeStart) / Mathf.Max(0.01f, visibleDistance - fadeStart)));

        label.text = Describe();
    }

    /// <summary>World-space top of the agent's visible body.</summary>
    private float TopOfBody(Transform agent)
    {
        Transform body = agent.Find("Body");

        if (body == null)
        {
            return agent.position.y;
        }

        float top = float.MinValue;

        foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
        {
            top = Mathf.Max(top, r.bounds.max.y);
        }

        return top > float.MinValue ? top : agent.position.y;
    }

    private string Describe()
    {
        if (shadow != null)
        {
            switch (shadow.CurrentState)
            {
                case ShadowAI.State.Frozen:
                    return "<color=#8BE29A>CHRONOLOGICAL SHADOW</color>\n<size=60%>frozen</size>";

                case ShadowAI.State.Flee:
                    return "<color=#C9B3FF>CHRONOLOGICAL SHADOW</color>\n<size=60%>fleeing the Orb</size>";

                case ShadowAI.State.SeekShard:
                    return "<color=#FF9E7A>CHRONOLOGICAL SHADOW</color>\n" +
                           "<size=60%>stealing a Time Shard - freeze it with the Orb</size>";

                default:
                    return "<color=#C9B3FF>CHRONOLOGICAL SHADOW</color>\n" +
                           "<size=60%>it cannot hurt you - it steals Time Shards</size>";
            }
        }

        if (warden != null)
        {
            switch (warden.CurrentState)
            {
                case WardenAI.State.Frozen:
                    return "<color=#8BE29A>TIME WARDEN</color>\n<size=60%>frozen</size>";

                case WardenAI.State.Chase:
                    return "<color=#FF7A72>TIME WARDEN</color>\n<size=60%>chasing you - it costs health and score if it reaches you.</size>" +
                           "<size=60%> Break line of sight, or freeze it with the Orb</size>";

                case WardenAI.State.Alert:
                case WardenAI.State.Search:
                    return "<color=#FFC46B>TIME WARDEN</color>\n<size=60%>searching - stay out of its cone</size>";

                default:
                    return "<color=#9FB4D6>TIME WARDEN</color>\n<size=60%>patrolling - stay out of its vision cone</size>";
            }
        }

        return "";
    }
}
