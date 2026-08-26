using System;
using UnityEngine;

/// <summary>
/// FrozenCity's core puzzle, read literally from the plan: "the tower bell
/// never rang, so the moment cannot continue." Return a gear to the tower
/// across eras - find it in the Past, install it in the Present, verify it
/// in the Future. Completing it is what lets the Chrono Hourglass appear;
/// before that, the city has not actually been freed.
/// </summary>
public sealed class GearPuzzle : MonoBehaviour
{
    public static GearPuzzle Instance { get; private set; }

    [Tooltip("Hidden until the puzzle is solved - the Chrono Hourglass pickup.")]
    [SerializeField] private GameObject rewardObject;

    public bool HasGear { get; private set; }
    public bool Installed { get; private set; }
    public bool Verified { get; private set; }

    public event Action Solved;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (rewardObject != null)
        {
            rewardObject.SetActive(false);
        }
    }

    /// <summary>Found in the Past.</summary>
    public void CollectGear()
    {
        HasGear = true;
    }

    /// <summary>Installed in the Present. Returns whether it actually happened.</summary>
    public bool TryInstall()
    {
        if (Installed || !HasGear || !InEra(TimeEra.Present))
        {
            return false;
        }

        Installed = true;
        return true;
    }

    /// <summary>Verified in the Future. Returns whether it actually happened.</summary>
    public bool TryVerify()
    {
        if (Verified || !Installed || !InEra(TimeEra.Future))
        {
            return false;
        }

        Verified = true;

        if (rewardObject != null)
        {
            rewardObject.SetActive(true);
        }

        Solved?.Invoke();
        return true;
    }

    private static bool InEra(TimeEra era)
    {
        return EraManager.Instance != null && EraManager.Instance.CurrentEra == era;
    }
}
