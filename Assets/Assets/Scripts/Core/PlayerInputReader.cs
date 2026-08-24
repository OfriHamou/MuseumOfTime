using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    [Header("Continuous Input")]
    [SerializeField] private Vector2 moveInput;
    [SerializeField] private Vector2 lookInput;

    [Header("State")]
    [SerializeField] private bool isRunning;
    [SerializeField] private bool isSlowTimeHeld;

    public Vector2 MoveInput => moveInput;
    public Vector2 LookInput => lookInput;
    public bool IsRunning => isRunning;
    public bool IsSlowTimeHeld => isSlowTimeHeld;

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
        if (context.performed)
        {
            Debug.Log("Jump pressed.");
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Debug.Log("Interact pressed.");
        }
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Debug.Log("Shoot pressed.");
        }
    }

    public void OnCameraToggle(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Debug.Log("Camera Toggle pressed.");
        }
    }

    public void OnPause(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Debug.Log("Pause pressed.");
        }
    }
}