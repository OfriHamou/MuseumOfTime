using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Receives input from Unity's New Input System and stores it for other
/// player scripts to read. This is the only place in the project that talks
/// to the Input System directly.
///
/// Continuous values (Move, Look, Run, SlowTime) are held for as long as the
/// control is active. One-shot values (Jump, Interact, ...) are true for a
/// single frame and cleared in LateUpdate, after every Update has run.
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
    private bool eraBackPressed;
    private bool eraForwardPressed;
    private bool journalPressed;

    public Vector2 MoveInput => moveInput;
    public Vector2 LookInput => lookInput;

    public bool IsRunning => isRunning;
    public bool IsSlowTimeHeld => isSlowTimeHeld;

    public bool JumpPressed => jumpPressed;
    public bool InteractPressed => interactPressed;
    public bool ShootPressed => shootPressed;
    public bool CameraTogglePressed => cameraTogglePressed;
    public bool PausePressed => pausePressed;

    /// <summary>Step one era towards the past (Q).</summary>
    public bool EraBackPressed => eraBackPressed;

    /// <summary>Step one era towards the future (R).</summary>
    public bool EraForwardPressed => eraForwardPressed;

    /// <summary>Open or close the Time Journal (Tab).</summary>
    public bool JournalPressed => journalPressed;

    // ---------------------------------------------------------------
    // Called by the PlayerInput component through Unity Events.
    // The method name must match the action name: OnMove <- "Move".
    // ---------------------------------------------------------------

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

    public void OnEraBack(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        eraBackPressed = true;
        LogAction("Era Back");
    }

    public void OnEraForward(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        eraForwardPressed = true;
        LogAction("Era Forward");
    }

    public void OnJournal(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        journalPressed = true;
        LogAction("Journal");
    }

    private void LateUpdate()
    {
        // One-frame actions are cleared after all regular Update methods ran,
        // so every script polling them during Update sees the same value.
        jumpPressed = false;
        interactPressed = false;
        shootPressed = false;
        cameraTogglePressed = false;
        pausePressed = false;
        eraBackPressed = false;
        eraForwardPressed = false;
        journalPressed = false;
    }

    private void OnDisable()
    {
        // Stop the player drifting if input is lost while a key is held,
        // for example when the game is paused or the window loses focus.
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        isSlowTimeHeld = false;
    }

    private void LogAction(string actionName)
    {
        if (showDebugMessages)
        {
            Debug.Log($"Input received: {actionName}", this);
        }
    }
}
