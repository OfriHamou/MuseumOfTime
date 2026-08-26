using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Owns the two gameplay cameras and switches between them.
///
/// The GDD specifies a third-person view. The assignment additionally requires
/// a first-person to third-person switch with two cameras besides the minimap,
/// so first person is Noa raising the Time Lens to her eye to read an exhibit
/// closely. Both are Cinemachine cameras; switching is done by priority so the
/// CinemachineBrain blends between them rather than cutting.
///
/// Mouse look is applied here rather than inside Cinemachine: yaw turns the
/// player, pitch tilts a pivot at head height. Both cameras follow that pivot,
/// so the two views stay consistent with each other and with movement.
/// </summary>
public sealed class PlayerCameraRig : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera firstPersonCamera;
    [SerializeField] private CinemachineCamera thirdPersonCamera;

    [Header("Look")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 0.12f;

    [Tooltip("How far Noa can look up and down, in degrees.")]
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Start-up")]
    [SerializeField] private bool startInFirstPerson;

    private const int ActivePriority = 20;
    private const int InactivePriority = 0;

    private PlayerInputReader inputReader;
    private float pitch;
    private bool isFirstPerson;

    /// <summary>True while the first-person camera is the live one.</summary>
    public bool IsFirstPerson => isFirstPerson;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();

        if (inputReader == null)
        {
            inputReader = GetComponentInParent<PlayerInputReader>();
        }

        isFirstPerson = startInFirstPerson;
        ApplyCameraPriorities();
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (inputReader == null)
        {
            return;
        }

        HandleLook();

        if (inputReader.CameraTogglePressed)
        {
            ToggleCamera();
        }
    }

    private void HandleLook()
    {
        Vector2 look = inputReader.LookInput;

        if (look.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // Mouse delta is already frame-relative, so it must NOT be scaled by
        // deltaTime; doing so makes sensitivity depend on framerate.
        transform.Rotate(Vector3.up, look.x * mouseSensitivity, Space.World);

        pitch = Mathf.Clamp(
            pitch - (look.y * mouseSensitivity),
            minPitch,
            maxPitch);

        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    /// <summary>Swaps between the first and third person cameras.</summary>
    public void ToggleCamera()
    {
        isFirstPerson = !isFirstPerson;
        ApplyCameraPriorities();
    }

    private void ApplyCameraPriorities()
    {
        // Cinemachine picks whichever camera has the highest priority; the
        // brain then blends from the old one to the new one.
        if (firstPersonCamera != null)
        {
            firstPersonCamera.Priority = isFirstPerson
                ? ActivePriority
                : InactivePriority;
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.Priority = isFirstPerson
                ? InactivePriority
                : ActivePriority;
        }
    }
}
