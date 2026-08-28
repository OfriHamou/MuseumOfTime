using UnityEngine;

/// <summary>
/// Turns any world-space visual to face the active camera. WorldSignpost
/// already does this, but only for objects that also carry a TextMeshPro
/// (it rewrites the label too) - the Temporal Seal riddle images are a plain
/// textured quad with no text component, so they need the billboarding on
/// its own.
/// </summary>
public sealed class Billboard : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            return;
        }

        // Yaw-only: a framed panel that also pitches to face a camera above
        // or below it reads as tilted/hung crooked. Turning to face the
        // player's horizontal direction only keeps it hanging flat on the
        // wall the way a real museum plaque would.
        Vector3 toCamera = transform.position - cam.transform.position;
        toCamera.y = 0f;

        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(toCamera);
    }
}
