using UnityEngine;

/// <summary>A door that needs a specific item before it will open.</summary>
public sealed class DoorInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private bool requiresTimeLens;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float openSpeed = 120f;

    private bool open;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    public string Prompt
    {
        get
        {
            if (!CanInteract)
            {
                return "Locked - the Time Lens would show the way";
            }

            return open ? "Close" : "Open";
        }
    }

    public bool CanInteract =>
        !requiresTimeLens ||
        (GameManager.Instance != null &&
         GameManager.Instance.State.hasTimeLens);

    private void Awake()
    {
        closedRotation = transform.rotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void Update()
    {
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            open ? openRotation : closedRotation,
            openSpeed * Time.deltaTime);
    }

    public void Interact(GameObject interactor)
    {
        open = !open;
    }
}
