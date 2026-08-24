using UnityEngine;

/// <summary>
/// Feeds Noa's Animator from the movement and input state. The Animator holds
/// no game logic of its own: it only reacts to the parameters set here, so
/// animation can never disagree with what the character is actually doing.
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerInputReader))]
public sealed class PlayerAnimatorDriver : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int GroundedId = Animator.StringToHash("IsGrounded");
    private static readonly int JumpId = Animator.StringToHash("JumpTrigger");
    private static readonly int InteractId = Animator.StringToHash("InteractTrigger");

    [SerializeField] private Animator animator;

    private PlayerController controller;
    private PlayerInputReader inputReader;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        inputReader = GetComponent<PlayerInputReader>();

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

        // Speed comes from the CharacterController's actual velocity, not the
        // raw input, so walking into a wall correctly reads as standing still.
        animator.SetFloat(SpeedId, controller.CurrentSpeed);
        animator.SetBool(GroundedId, controller.IsGrounded);

        if (inputReader.JumpPressed)
        {
            animator.SetTrigger(JumpId);
        }

        if (inputReader.InteractPressed)
        {
            animator.SetTrigger(InteractId);
        }
    }
}
