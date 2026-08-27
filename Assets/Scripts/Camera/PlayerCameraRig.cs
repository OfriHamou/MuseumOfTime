using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

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
    /// <summary>
    /// Degrees of rotation per unit of mouse delta.
    ///
    /// This was 0.12, which is slow enough that turning round meant dragging
    /// the mouse across the mat several times - and on a desk that runs out of
    /// room, which reads as the view refusing to turn any further even though
    /// nothing in the code limits it. Making a small movement cover a large
    /// angle is what makes looking around feel like looking around.
    /// </summary>
    [Range(0.05f, 1f)]
    [SerializeField] private float mouseSensitivity = 0.35f;

    [Tooltip("How far Noa can look up and down, in degrees.")]
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Start-up")]
    [SerializeField] private bool startInFirstPerson;

    private const int ActivePriority = 20;
    private const int InactivePriority = 0;

    private PlayerInputReader inputReader;
    private PauseMenuController pauseMenu;
    private float pitch;

    /// <summary>Last reported pointer position, used to tell a loose pointer
    /// from one the OS is holding still.</summary>
    private Vector2 lastPointerPosition;
    private bool isFirstPerson;

    /// <summary>True while the first-person camera is the live one.</summary>
    public bool IsFirstPerson => isFirstPerson;

    /// <summary>
    /// True while gameplay should own the mouse - focused, and not paused.
    /// </summary>
    public bool CursorCaptureWanted { get; private set; }

    /// <summary>
    /// How many times the lock has had to be re-taken after something else
    /// dropped it. Exposed so a test can prove the rig recovers rather than
    /// locking once at startup and never again.
    /// </summary>
    public int CursorRecaptureCount { get; private set; }

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
        CaptureCursor();
    }

    private void OnDisable()
    {
        ReleaseCursor();
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

    /// <summary>
    /// Playtest escape hatch. Defaults to false and nothing in the game ever
    /// sets it - it exists purely so a human or a tool driving the Editor can
    /// steer the camera.
    ///
    /// A locked cursor takes its look delta from raw mouse input, which
    /// SetCursorPos does not generate: the lock snaps the pointer back to the
    /// centre within the same frame, so a synthetic absolute move nets a delta
    /// of exactly zero and the view will not turn at all. A real mouse is
    /// unaffected. Releasing the lock makes position changes readable as
    /// deltas again, which is what allows the game to be driven and played
    /// through end to end rather than only asserted about.
    /// </summary>
    public static bool FreeCursorForPlaytest;

    /// <summary>
    /// Clears the playtest flag at the start of every run.
    ///
    /// It is a static, so without this it survives for the lifetime of the
    /// Editor's script domain - set it once to steer the camera by hand and it
    /// stays set through every later play session and every test run in that
    /// domain. It did exactly that: four CursorLockTests failed because the rig
    /// had been told not to capture the cursor half an hour earlier.
    ///
    /// SubsystemRegistration runs before the first scene loads on every entry
    /// to play mode, which is the right place to reset statics that must not
    /// carry over.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlaytestFlag()
    {
        FreeCursorForPlaytest = false;
    }

    private void LateUpdate()
    {
        EnforceCursorState();
    }

    /// <summary>
    /// Re-applies the cursor lock whenever gameplay should own the mouse.
    ///
    /// Setting Cursor.lockState once in OnEnable is not enough, and this was
    /// the bug that made the game unplayable: Unity DROPS the lock on its own
    /// in several situations and never restores it.
    ///
    ///   - Alt-tabbing away, or clicking outside a windowed player, releases
    ///     it. Focus coming back does not re-apply it.
    ///   - In the Editor, pressing Escape releases it unconditionally - and
    ///     Escape is also the Pause binding.
    ///   - A lock requested before the window actually has focus (which is
    ///     exactly when OnEnable runs on the first frame of Play) is silently
    ///     dropped.
    ///
    /// Once released, nothing re-locked it, so the mouse ran off the window
    /// and looking around stopped working entirely for the rest of the
    /// session. Re-asserting it every frame is the standard fix.
    ///
    /// It deliberately does NOT fight the pause menu or an unfocused window,
    /// so Escape still frees the mouse to click Resume, and alt-tab still
    /// works.
    /// </summary>
    private void EnforceCursorState()
    {
        // Paused is the ONLY reason gameplay gives the mouse back.
        //
        // A previous version also released whenever Application.isFocused was
        // false. That was wrong in two ways: Unity already drops the OS lock
        // by itself when the window loses focus, so forcing it achieves
        // nothing; and isFocused reads false during the first frames of a
        // freshly launched player, so the rig was actively fighting its own
        // lock exactly when the player was trying to start playing.
        CursorCaptureWanted = !IsPaused() && !FreeCursorForPlaytest;

        if (CursorCaptureWanted)
        {
            if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
            {
                CaptureCursor();
                CursorRecaptureCount++;
            }
        }
        else if (Cursor.lockState != CursorLockMode.None)
        {
            ReleaseCursor();
        }

        KeepPointerOffTheEdge();
    }

    /// <summary>
    /// Fallback for when the OS refuses to hold the lock: warp the pointer
    /// back to the middle of the window every frame.
    ///
    /// Re-asserting CursorLockMode.Locked is the correct fix and is what
    /// normally runs, but it is not guaranteed - the Editor only honours a
    /// lock while the Game view is the focused pane, and some window managers
    /// and remote-desktop sessions drop it outright. Whenever that happens the
    /// pointer is free to slide to the edge of the physical screen, and at the
    /// edge the OS stops producing movement: Mouse/delta goes to zero and the
    /// camera simply stops turning part-way round. That is the "I can't look
    /// all the way around, the scrolling stops" symptom exactly.
    ///
    /// Keeping the pointer pinned to the centre means it can never reach an
    /// edge, so delta keeps arriving and yaw stays unbounded whether or not
    /// the lock is actually held.
    /// </summary>
    private void KeepPointerOffTheEdge()
    {
        // This is a FALLBACK, and it must stay one.
        //
        // When the OS is genuinely holding the lock the pointer is pinned at
        // the centre of the window and cannot reach an edge, so there is
        // nothing here to fix. Running anyway is not merely redundant - it is
        // the bug. On any frame where the reported position sits outside the
        // margin this method warps AND calls SuppressLookThisFrame, which
        // throws away the very look input it exists to protect. Small mouse
        // movements stay inside the margin and work; large sweeps get eaten.
        // That is precisely the reported symptom: the view turns a little and
        // then refuses to turn any further.
        //
        // Found by playing the game rather than by reasoning about it. An
        // earlier version of this method did return early on a held lock; I
        // removed that guard on the strength of an Input System issue saying
        // the lock can be reported as held while delta has already died at the
        // window edge. That reading was plausible and it was wrong here:
        // driving the real build showed the lock genuinely holds - the OS
        // cursor reports dead centre after every move - so the guard was never
        // the thing breaking the look. Removing it was.
        if (!CursorCaptureWanted || Cursor.lockState == CursorLockMode.Locked)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 position = mouse.position.ReadValue();

        Vector2 travelled = position - lastPointerPosition;
        lastPointerPosition = position;

        // Two conditions, and the second one matters as much as the first.
        //
        //   1. The pointer is drifting out towards an edge.
        //   2. It is genuinely moving.
        //
        // Without (2) this is dangerous rather than merely redundant. A held
        // lock pins the OS pointer, but what Unity REPORTS for its position
        // while locked is not guaranteed to be the window centre - some
        // platforms keep returning the last free position instead. If that
        // stale value happens to sit outside the margin, a position-only test
        // would warp and suppress look on every single frame, which would not
        // limit the view, it would disable it completely.
        //
        // A pointer that is not moving needs no rescue, so requiring real
        // travel makes this inert whenever the lock is doing its job and
        // active only when the pointer is actually running loose.
        float margin = Mathf.Min(Screen.width, Screen.height) * 0.25f;

        bool driftingOut = Mathf.Abs(position.x - centre.x) >= margin ||
                           Mathf.Abs(position.y - centre.y) >= margin;

        bool actuallyMoving = travelled.sqrMagnitude > 0.01f;

        if (!driftingOut || !actuallyMoving)
        {
            return;
        }

        mouse.WarpCursorPosition(centre);

        // A warp can surface as one large synthetic delta on the next read.
        // Dropping that frame's look keeps the view from snapping.
        if (inputReader != null)
        {
            inputReader.SuppressLookThisFrame();
        }
    }

    /// <summary>
    /// Focus coming back is the moment Unity has just discarded the lock, so
    /// it is re-taken immediately rather than a frame later.
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && enabled)
        {
            EnforceCursorState();
        }
    }

    private bool IsPaused()
    {
        if (pauseMenu == null)
        {
            pauseMenu = FindAnyObjectByType<PauseMenuController>();
        }

        return pauseMenu != null && pauseMenu.IsPaused;
    }

    private static void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
