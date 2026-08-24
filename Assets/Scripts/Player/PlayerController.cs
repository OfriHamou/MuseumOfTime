using UnityEngine;

/// <summary>
/// Controls basic player movement using the CharacterController.
/// Input comes from PlayerInputReader.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedForce = -2f;

    private CharacterController characterController;
    private PlayerInputReader inputReader;

    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        inputReader = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
        HandleMovement();
        HandleGravity();
    }

    private void HandleMovement()
    {
        Vector2 input = inputReader.MoveInput;

        Vector3 movement = new Vector3(
            input.x,
            0f,
            input.y
        );

        float speed = inputReader.IsRunning
            ? runSpeed
            : walkSpeed;

        characterController.Move(
            movement * speed * Time.deltaTime
        );
    }

    private void HandleGravity()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedForce;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 gravityMovement = new Vector3(
            0f,
            verticalVelocity,
            0f
        );

        characterController.Move(
            gravityMovement * Time.deltaTime
        );
    }
}