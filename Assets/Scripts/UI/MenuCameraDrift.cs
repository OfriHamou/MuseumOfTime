using UnityEngine;

/// <summary>
/// A slow rotation over the menu vignette. Cheap, and it is what the plan
/// calls out as selling the trailer's opening shot (G1/G2) - not a gameplay
/// system, so it earns exactly one field.
/// </summary>
public sealed class MenuCameraDrift : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 1.5f;

    private void Update()
    {
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
    }
}
