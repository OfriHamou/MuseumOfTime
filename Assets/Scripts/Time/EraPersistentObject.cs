using UnityEngine;

/// <summary>
/// An object whose position carries FORWARD through time. Move the cart in
/// the past and it has been moved in the present and the future as well.
///
/// This is the mechanic the GDD's own example puzzle rests on: shifting a
/// cart in the past opens a route in the present but blocks a different exit
/// in the future. Without it, era switching is only three sets of scenery,
/// and the game loses the thing that makes it interesting.
/// </summary>
public sealed class EraPersistentObject : MonoBehaviour
{
    [Tooltip("Where this object stands in each era: Past, Present, Future.")]
    [SerializeField] private Vector3[] eraPositions = new Vector3[3];

    [Tooltip("Eras after the current one inherit any change made here.")]
    [SerializeField] private bool propagatesForward = true;

    private bool captured;

    private void OnEnable()
    {
        Capture();

        if (EraManager.Instance != null)
        {
            EraManager.Instance.EraChanged += OnEraChanged;
            Apply(EraManager.Instance.CurrentEra);
        }
    }

    private void OnDisable()
    {
        if (EraManager.Instance != null)
        {
            EraManager.Instance.EraChanged -= OnEraChanged;
        }
    }

    /// <summary>Seeds any era left unset in the Inspector with the start position.</summary>
    private void Capture()
    {
        if (captured)
        {
            return;
        }

        if (eraPositions == null || eraPositions.Length < 3)
        {
            eraPositions = new Vector3[3];
        }

        for (int i = 0; i < eraPositions.Length; i++)
        {
            if (eraPositions[i] == Vector3.zero)
            {
                eraPositions[i] = transform.position;
            }
        }

        captured = true;
    }

    /// <summary>
    /// Records where the object has been pushed to in the current era, and
    /// writes that through to every later era.
    /// </summary>
    public void CommitCurrentPosition()
    {
        if (EraManager.Instance == null)
        {
            return;
        }

        Capture();

        int era = (int)EraManager.Instance.CurrentEra;
        eraPositions[era] = transform.position;

        if (!propagatesForward)
        {
            return;
        }

        // Causality: a change made in the past is already true later on.
        for (int later = era + 1; later < eraPositions.Length; later++)
        {
            eraPositions[later] = transform.position;
        }
    }

    private void OnEraChanged(TimeEra era)
    {
        Apply(era);
    }

    private void Apply(TimeEra era)
    {
        Capture();
        transform.position = eraPositions[(int)era];
    }

    /// <summary>Where this object stands in a given era.</summary>
    public Vector3 PositionIn(TimeEra era)
    {
        Capture();
        return eraPositions[(int)era];
    }

    /// <summary>Moves the object and commits the change forward in one step.</summary>
    public void MoveTo(Vector3 position)
    {
        transform.position = position;
        CommitCurrentPosition();
    }
}
