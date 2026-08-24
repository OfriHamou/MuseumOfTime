using UnityEngine;

/// <summary>
/// Moves Noa with the CharacterController, relative to wherever the camera is
/// looking. Input arrives through PlayerInputReader; this script never touches
/// the Input System itself.
///
/// Noa's facing is owned by PlayerCameraRig, which turns her with mouse yaw.
/// This script must not rotate the transform as well: two scripts rotating the
/// same transform in the same frame fight, and the result depends on script
/// execution order.
///
/// "Camera-relative" is the important part. Building the move vector straight
/// from the raw input, as in new Vector3(input.x, 0, input.y), always pushes
/// the player along the world axes: W walks towards world +Z no matter which
/// way the player is facing. The fix is to project the camera's forward and
/// right onto the horizontal plane and steer along those instead.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputReader))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;

    [Header("Gravity and jumping")]
    [SerializeField] private float gravity = -20f;

    [Tooltip("Small downward force while grounded, so the controller stays " +
             "pinned to the floor instead of stepping off every slope.")]
    [SerializeField] private float groundedForce = -2f;

    [SerializeField] private float jumpHeight = 1.2f;

    [Tooltip("Grace period after walking off a ledge during which a jump is " +
             "still allowed. Makes jumping feel fair rather than twitchy.")]
    [SerializeField] private float coyoteTime = 0.15f;

    [Header("Stairs and slopes")]
    [Tooltip("Applied to the CharacterController on Awake so the museum " +
             "staircase in Step 2.1 is climbable.")]
    [SerializeField] private float stepOffset = 0.35f;

    [SerializeField] private float slopeLimit = 50f;

    private CharacterController characterController;
    private PlayerInputReader inputReader;
    private Transform cameraTransform;

    private float verticalVelocity;
    private float timeSinceGrounded;

    /// <summary>True while the controller is touching the ground.</summary>
    public bool IsGrounded => characterController.isGrounded;

    /// <summary>Horizontal speed in metres per second, for the Animator.</summary>
    public float CurrentSpeed
    {
        get
        {
            Vector3 velocity = characterController.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        inputReader = GetComponent<PlayerInputReader>();

        characterController.stepOffset = stepOffset;
        characterController.slopeLimit = slopeLimit;
    }

    private void Start()
    {
        // Resolved late: the camera rig creates the active camera in Awake.
        CacheCamera();
    }

    private void CacheCamera()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (cameraTransform == null)
        {
            CacheCamera();
        }

        HandleMovement();
        HandleGravityAndJump();
    }

    private void HandleMovement()
    {
        Vector2 input = inputReader.MoveInput;

        Vector3 direction = ToCameraRelativeDirection(input);

        float speed = inputReader.IsRunning ? runSpeed : walkSpeed;

        characterController.Move(direction * speed * Time.deltaTime);
    }

    /// <summary>
    /// Turns 2D input into a world direction along the camera's own axes,
    /// flattened so that looking up or down never pushes Noa into the floor.
    /// </summary>
    private Vector3 ToCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        if (cameraTransform == null)
        {
            // No camera yet: fall back to world axes rather than freezing.
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction = (forward * input.y) + (right * input.x);

        // Diagonals would otherwise be faster than the cardinal directions.
        return Vector3.ClampMagnitude(direction, 1f);
    }


    private void HandleGravityAndJump()
    {
        if (characterController.isGrounded)
        {
            timeSinceGrounded = 0f;

            if (verticalVelocity < 0f)
            {
                verticalVelocity = groundedForce;
            }
        }
        else
        {
            timeSinceGrounded += Time.deltaTime;
        }

        if (inputReader.JumpPressed && CanJump())
        {
            // v = sqrt(-2 * g * h) gives exactly the requested peak height.
            verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
            timeSinceGrounded = coyoteTime;
        }

        verticalVelocity += gravity * Time.deltaTime;

        characterController.Move(
            new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
    }

    private bool CanJump()
    {
        return characterController.isGrounded || timeSinceGrounded < coyoteTime;
    }
}
