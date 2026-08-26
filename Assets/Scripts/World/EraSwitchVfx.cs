using UnityEngine;

/// <summary>
/// A burst of particles at Noa's position on every era switch - one of the
/// five particle effects the plan lists ("era-switch shockwave"). The other
/// four (shard collection, fracture dust, orb trail, Shadow drift) are left
/// for a dedicated art/VFX pass - see Phase7_Unity_Walkthrough.md.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class EraSwitchVfx : MonoBehaviour
{
    [SerializeField] private Transform target;

    private ParticleSystem particles;

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    private void OnEnable()
    {
        if (EraManager.Instance != null)
        {
            EraManager.Instance.EraChanged += OnEraChanged;
        }
    }

    private void OnDisable()
    {
        if (EraManager.Instance != null)
        {
            EraManager.Instance.EraChanged -= OnEraChanged;
        }
    }

    private void OnEraChanged(TimeEra era)
    {
        if (target != null)
        {
            transform.position = target.position;
        }

        particles.Play();
    }
}
