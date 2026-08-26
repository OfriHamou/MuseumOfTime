using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Receives input from Unity's New Input System and stores it for other
/// player scripts to read. This is the only place in the project that talks
/// to the Input System directly.
///
/// Actions are looked up and subscribed to IN CODE (see Awake/OnEnable), not
/// wired by hand in the Inspector. Hand-wiring twenty Unity Events is easy to
/// get wrong and impossible to verify by reading the code: this project had
/// Move connected to nothing and Jump connected to OnRun.
///
/// The public OnXxx methods are kept so that any Unity Events already wired on
/// the PlayerInput component still work. They are safe to call twice: the
/// continuous values are plain assignments, and the one-shot flags are only
/// ever set true here and cleared in LateUpdate.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
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

    private PlayerInput playerInput;
    private InputActionMap playerMap;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction runAction;
    private InputAction slowTimeAction;
    private InputAction jumpAction;
    private InputAction interactAction;
    private InputAction shootAction;
    private InputAction cameraToggleAction;
    private InputAction pauseAction;
    private InputAction eraBackAction;
    private InputAction eraForwardAction;
    private InputAction journalAction;

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

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        if (playerInput.actions == null)
        {
            Debug.LogError(
                "PlayerInput has no Actions asset assigned. " +
                "Drag MuseumInputActions into its Actions field.",
                this);

            return;
        }

        // "true" makes these throw a clear exception naming the missing action
        // instead of failing silently later.
        playerMap = playerInput.actions.FindActionMap("Player", true);

        moveAction = playerMap.FindAction("Move", true);
        lookAction = playerMap.FindAction("Look", true);
        runAction = playerMap.FindAction("Run", true);
        slowTimeAction = playerMap.FindAction("SlowTime", true);
        jumpAction = playerMap.FindAction("Jump", true);
        interactAction = playerMap.FindAction("Interact", true);
        shootAction = playerMap.FindAction("Shoot", true);
        cameraToggleAction = playerMap.FindAction("CameraToggle", true);
        pauseAction = playerMap.FindAction("Pause", true);
        eraBackAction = playerMap.FindAction("EraBack", true);
        eraForwardAction = playerMap.FindAction("EraForward", true);
        journalAction = playerMap.FindAction("Journal", true);
    }

    private void OnEnable()
    {
        if (playerMap == null)
        {
            return;
        }

        // Held values need both events: "performed" when the control moves,
        // "canceled" when it returns to rest so the value goes back to zero.
        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        lookAction.performed += OnLook;
        lookAction.canceled += OnLook;
        runAction.performed += OnRun;
        runAction.canceled += OnRun;
        slowTimeAction.performed += OnSlowTime;
        slowTimeAction.canceled += OnSlowTime;

        // One-shot actions only care about the moment they fire.
        jumpAction.performed += OnJump;
        interactAction.performed += OnInteract;
        shootAction.performed += OnShoot;
        cameraToggleAction.performed += OnCameraToggle;
        pauseAction.performed += OnPause;
        eraBackAction.performed += OnEraBack;
        eraForwardAction.performed += OnEraForward;
        journalAction.performed += OnJournal;

        playerMap.Enable();
    }

    private void OnDisable()
    {
        if (playerMap == null)
        {
            return;
        }

        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMove;
        lookAction.performed -= OnLook;
        lookAction.canceled -= OnLook;
        runAction.performed -= OnRun;
        runAction.canceled -= OnRun;
        slowTimeAction.performed -= OnSlowTime;
        slowTimeAction.canceled -= OnSlowTime;

        jumpAction.performed -= OnJump;
        interactAction.performed -= OnInteract;
        shootAction.performed -= OnShoot;
        cameraToggleAction.performed -= OnCameraToggle;
        pauseAction.performed -= OnPause;
        eraBackAction.performed -= OnEraBack;
        eraForwardAction.performed -= OnEraForward;
        journalAction.performed -= OnJournal;

        // Stop the player drifting if input is lost while a key is held,
        // for example when the game is paused or the window loses focus.
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        isSlowTimeHeld = false;
    }

    // ---------------------------------------------------------------
    // Public so that any Unity Events still wired on the PlayerInput
    // component keep working. Calling them twice is harmless.
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
        if (!context.performed) { return; }
        jumpPressed = true;
        LogAction("Jump");
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) { return; }
        interactPressed = true;
        LogAction("Interact");
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (!context.performed) { return; }
        shootPressed = true;
        LogAction("Shoot");
    }

    public void OnCameraToggle(InputAction.CallbackContext context)
    {
        if (!context.performed) { return; }
        cameraTogglePressed = true;
        LogAction("Camera Toggle");
    }

    public void OnPause(InputAction.CallbackContext context)
    {
        if (!context.performed) { return; }
        pausePressed = true;
        LogAction("Pause");
    }

    public void OnEraBack(InputAction.CallbackContext context)
    {
        if (!context.performed) { return; }
        eraBackPressed = true;
        LogAction("Era Back");
    }

    public void OnEraForward(InputAction.CallbackContext context)
    {
        if (!context.performed) { return; }
        eraForwardPressed = true;
        LogAction("Era Forward");
    }

    public void OnJournal(InputAction.CallbackContext context)
    {
        if (!context.performed) { return; }
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

    private void LogAction(string actionName)
    {
        if (showDebugMessages)
        {
            Debug.Log($"Input received: {actionName}", this);
        }
    }
}
