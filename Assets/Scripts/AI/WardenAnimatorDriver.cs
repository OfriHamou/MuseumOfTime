using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Feeds the Warden's Animator from its AI state. As with Noa, the Animator
/// holds no logic of its own: it only reacts, so animation can never disagree
/// with what the AI is actually doing.
/// </summary>
[RequireComponent(typeof(WardenAI))]
public sealed class WardenAnimatorDriver : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int AlertId = Animator.StringToHash("AlertLevel");
    private static readonly int FrozenId = Animator.StringToHash("IsFrozen");

    [SerializeField] private Animator animator;

    private WardenAI warden;
    private NavMeshAgent agent;

    private void Awake()
    {
        warden = GetComponent<WardenAI>();
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        // Real velocity, not the intended destination, so an agent stuck
        // against a wall reads as standing still.
        animator.SetFloat(SpeedId, agent != null ? agent.velocity.magnitude : 0f);

        // Exactly the value the detection meter uses.
        animator.SetFloat(AlertId, warden.DetectionLevel);

        animator.SetBool(FrozenId, warden.CurrentState == WardenAI.State.Frozen);
    }
}
