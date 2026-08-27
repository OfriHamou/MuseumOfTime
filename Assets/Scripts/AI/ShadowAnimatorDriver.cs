using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Feeds the Chronological Shadow's Animator from its AI state, the same way
/// <see cref="WardenAnimatorDriver"/> does for the Warden: the Animator only
/// ever reacts, so the animation cannot disagree with what the AI is doing.
///
/// A separate component rather than a shared one because WardenAnimatorDriver
/// requires a WardenAI and reads its detection meter, which the Shadow has no
/// equivalent of - it flees rather than hunts.
/// </summary>
[RequireComponent(typeof(ShadowAI))]
public sealed class ShadowAnimatorDriver : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int FrozenId = Animator.StringToHash("IsFrozen");

    [SerializeField] private Animator animator;

    private ShadowAI shadow;
    private NavMeshAgent agent;

    private void Awake()
    {
        shadow = GetComponent<ShadowAI>();
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

        animator.SetFloat(SpeedId, agent != null ? agent.velocity.magnitude : 0f);
        animator.SetBool(FrozenId, shadow != null && shadow.CurrentState == ShadowAI.State.Frozen);
    }
}
