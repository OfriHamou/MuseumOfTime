using UnityEngine;

/// <summary>
/// Marks where the objective actually is - on the minimap and in the world.
///
/// The objective line said what to do ("leave the museum") and, after a
/// rewrite, roughly where ("the exit is north of the office, on this balcony").
/// Neither is a location. A player who has not already learned the building
/// still has to search it, and the most reasonable guess - walk out of the
/// front entrance - used to drop them out of the world entirely.
///
/// So point at it. A bright pip on the minimap says which way to walk, and a
/// beam of light at the same spot says which thing to walk into once it is on
/// screen. Between them there is nothing left to guess.
///
/// The target is resolved from live scene objects rather than hand-placed
/// coordinates, so it cannot drift out of step with the objective text or with
/// where the builders actually put things.
/// </summary>
public sealed class ObjectiveWaypoint : MonoBehaviour
{
    [Tooltip("The pip drawn on the minimap. Must be on the Minimap layer, or " +
             "it shows up in the player's view instead.")]
    [SerializeField] private Transform minimapPip;

    [Tooltip("The beam of light standing at the objective, in the world.")]
    [SerializeField] private Transform worldBeacon;

    [Tooltip("How far away the beam stays visible. Close up it is just in " +
             "the way - by then the thing itself is on screen.")]
    [SerializeField] private float beaconFadeDistance = 4f;

    /// <summary>Where the waypoint currently points, for tests.</summary>
    public Transform Target { get; private set; }

    private Transform player;
    private float nextResolveAt;

    private void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");

        if (found != null)
        {
            player = found.transform;
        }

        Resolve();
    }

    private void Update()
    {
        // Re-resolving every frame would mean several FindObjectsByType calls
        // per frame for something that changes a handful of times per scene.
        if (Time.unscaledTime >= nextResolveAt)
        {
            nextResolveAt = Time.unscaledTime + 0.5f;
            Resolve();
        }

        bool haveTarget = Target != null;

        if (minimapPip != null)
        {
            minimapPip.gameObject.SetActive(haveTarget);

            if (haveTarget)
            {
                // Held above the floor so it draws over the map geometry.
                minimapPip.position = new Vector3(
                    Target.position.x, Target.position.y + 6f, Target.position.z);
            }
        }

        if (worldBeacon == null)
        {
            return;
        }

        bool showBeam = haveTarget &&
            (player == null ||
             Vector3.Distance(player.position, Target.position) > beaconFadeDistance);

        worldBeacon.gameObject.SetActive(showBeam);

        if (showBeam)
        {
            worldBeacon.position = Target.position;
        }
    }

    /// <summary>
    /// Works out what the player is currently supposed to be walking towards.
    /// Mirrors ObjectiveTracker's steps, but resolves to real objects.
    /// </summary>
    private void Resolve()
    {
        GameState state = GameManager.Instance != null
            ? GameManager.Instance.State
            : null;

        if (state == null)
        {
            Target = null;
            return;
        }

        switch (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
        {
            case "MuseumNight":
                Target = state.hasTimeLens
                    ? FindExit()
                    : FindFirstActive<ItemPickup>();
                return;

            case "FrozenCity":
                Target = ResolveFrozenCity(state);
                return;

            case "ClockCore":
                Collector collector = Object.FindFirstObjectByType<Collector>();
                Target = collector != null && !collector.IsDefeated
                    ? collector.transform
                    : null;
                return;

            default:
                Target = null;
                return;
        }
    }

    private Transform ResolveFrozenCity(GameState state)
    {
        if (state.hasChronoHourglass)
        {
            return FindExit();
        }

        GearPuzzle puzzle = GearPuzzle.Instance;

        if (puzzle == null)
        {
            return null;
        }

        if (puzzle.Verified)
        {
            // The reward is revealed; point at it.
            return FindFirstActive<ItemPickup>();
        }

        if (puzzle.HasGear || puzzle.Installed)
        {
            return FindByTypeName("GearSocket");
        }

        return FindByTypeName("GearPickup");
    }

    private static Transform FindExit()
    {
        var exit = Object.FindFirstObjectByType<SceneExitTrigger>();
        return exit != null ? exit.transform : null;
    }

    private static Transform FindFirstActive<T>() where T : Component
    {
        foreach (T candidate in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.activeInHierarchy)
            {
                return candidate.transform;
            }
        }

        return null;
    }

    private static Transform FindByTypeName(string typeName)
    {
        System.Type type = typeof(GearPuzzle).Assembly.GetType(typeName);

        if (type == null)
        {
            return null;
        }

        Object[] found = Object.FindObjectsByType(type, FindObjectsSortMode.None);

        foreach (Object candidate in found)
        {
            var component = candidate as Component;

            if (component != null && component.gameObject.activeInHierarchy)
            {
                return component.transform;
            }
        }

        return null;
    }
}
