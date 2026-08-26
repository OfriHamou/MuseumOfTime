using UnityEngine;

/// <summary>
/// Follows Noa from directly above and rotates with her heading, so "up" on
/// the minimap always means "ahead" - that is the orientation T18 actually
/// asks for. LateUpdate, so it settles after the player has moved this frame.
/// </summary>
public sealed class MinimapController : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float height = 30f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 position = target.position;
        position.y += height;
        transform.position = position;

        transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
    }
}
