using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Receives input from Unity's New Input System.
/// Other player scripts will read the stored values from this component.
/// </summary>
public sealed class PlayerInputReader : MonoBehaviour
{
    [Header("Continuous Input")]
    [SerializeField] private Vector2 moveInput;
    [SerializeField] private Vector2 lookInput;
    [SerializeField] private bool isRunning;
    [SerializeField] private bool isSlowTimeHeld;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    private bool jumpPressed;
    private bool interactPressed;
    private bool shootPressed;
    private bool cameraTogglePressed;
    private bool pausePressed;

    public Vector2 MoveInput => moveInput;
    public Vector2 LookInput => lookInput;

    public bool IsRunning => isRunning;
    public bool IsSlowTimeHeld => isSlowTimeHeld;

    public bool JumpPressed => jumpPressed;
    public bool InteractPressed => interactPressed;
    public bool ShootPressed => shootPressed;
    public bool CameraTogglePressed => cameraTogglePressed;
    public bool PausePressed => pausePressed;

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        isRunning = context.ReadValueAsButton();
    }

    public void OnSlowTime(InputAction.CallbackContext context)
    {
        isSlowTimeHeld = context.ReadValueAsButton();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        jumpPressed = true;
        LogAction("Jump");
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        interactPressed = true;
        LogAction("Interact");
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        shootPressed = true;
        LogAction("Shoot");
    }

    public void OnCameraToggle(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        cameraTogglePressed = true;
        LogAction("Camera Toggle");
    }

    public void OnPause(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        pausePressed = true;
        LogAction("Pause");
    }

    private void LateUpdate()
    {
        // One-frame actions are reset after all regular Update methods ran.
        jumpPressed = false;
        interactPressed = false;
        shootPressed = false;
        cameraTogglePressed = false;
        pausePressed = false;
    }

    private void LogAction(string actionName)
    {
        if (showDebugMessages)
        {
            Debug.Log($"Input received: {actionName}", this);
        }
    }
}