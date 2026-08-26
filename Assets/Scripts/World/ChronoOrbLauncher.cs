using UnityEngine;

/// <summary>
/// Throws Chrono Orbs on the Shoot input. Spawns ahead of the active camera
/// so the orb goes where the player is looking in both camera modes.
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public sealed class ChronoOrbLauncher : MonoBehaviour
{
    [SerializeField] private GameObject orbPrefab;
    [SerializeField] private float launchForce = 14f;
    [SerializeField] private float cooldown = 0.4f;
    [SerializeField] private float energyCost = 5f;

    [Tooltip("How far in front of the camera the orb appears, so it does " +
             "not spawn inside Noa's own collider.")]
    [SerializeField] private float muzzleDistance = 1.2f;

    private PlayerInputReader inputReader;
    private float nextAllowedTime;

    /// <summary>Number of orbs thrown this session. Used by the playtest.</summary>
    public int ThrownCount { get; private set; }

    /// <summary>The most recent orb, or null once it has despawned.</summary>
    public ChronoOrb LastOrb { get; private set; }

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
        if (!inputReader.ShootPressed)
        {
            return;
        }

        Throw();
    }

    /// <summary>Throws one orb, if the cooldown and energy allow it.</summary>
    public bool Throw()
    {
        if (orbPrefab == null || Camera.main == null)
        {
            return false;
        }

        // Unscaled, so the cooldown is not itself slowed by the Hourglass.
        if (Time.unscaledTime < nextAllowedTime)
        {
            return false;
        }

        if (GameManager.Instance != null &&
            !GameManager.Instance.SpendEnergy(energyCost))
        {
            return false;
        }

        Transform cam = Camera.main.transform;
        Vector3 spawn = cam.position + (cam.forward * muzzleDistance);

        GameObject orb = Instantiate(orbPrefab, spawn, cam.rotation);
        LastOrb = orb.GetComponent<ChronoOrb>();
        LastOrb?.Launch(cam.forward, launchForce);

        nextAllowedTime = Time.unscaledTime + cooldown;
        ThrownCount++;
        return true;
    }
}
