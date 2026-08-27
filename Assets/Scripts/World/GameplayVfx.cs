using UnityEngine;

/// <summary>
/// Step 7.2 particle effects that fire on transient world events rather than
/// riding a moving object: the shard-collection sparkle and the fracture
/// dust burst. (The era-switch shockwave is EraSwitchVfx; the orb trail and
/// the Chronological Shadow drift ride their own objects and are attached by
/// AudioAndVfxBuilder.)
///
/// Read-only observer of the existing systems, same as AudioManager:
/// subscribes to GameManager.StateChanged for the shard sparkle, polls the
/// FracturedObject list for the dust. No Phase 3/4 source file changed.
/// </summary>
public sealed class GameplayVfx : MonoBehaviour
{
    private ParticleSystem shardBurst;
    private ParticleSystem fractureBurst;

    private Transform player;
    private FracturedObject[] fractures;
    private bool[] wasBroken;
    private int lastShardCount;

    /// <summary>Exposed so a wiring test can confirm the sparkle actually played.</summary>
    public ParticleSystem ShardBurst => shardBurst;

    /// <summary>Exposed so a wiring test can confirm the dust actually played.</summary>
    public ParticleSystem FractureBurst => fractureBurst;

    private void Awake()
    {
        shardBurst = CreateBurst("ShardBurst", new Color(1f, 0.9f, 0.4f), 20, 0.12f);
        fractureBurst = CreateBurst("FractureBurst", new Color(0.7f, 0.7f, 0.72f), 35, 0.18f);
    }

    private void Start()
    {
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
        {
            player = playerGo.transform;
        }

        fractures = FindObjectsByType<FracturedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        wasBroken = new bool[fractures.Length];

        if (GameManager.Instance != null)
        {
            lastShardCount = GameManager.Instance.State.timeShards;
            GameManager.Instance.StateChanged += OnStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged -= OnStateChanged;
        }
    }

    private void Update()
    {
        if (fractures == null)
        {
            return;
        }

        for (int i = 0; i < fractures.Length; i++)
        {
            if (fractures[i] == null)
            {
                continue;
            }

            if (fractures[i].IsBroken && !wasBroken[i])
            {
                fractureBurst.transform.position = fractures[i].transform.position;
                fractureBurst.Play();
            }

            wasBroken[i] = fractures[i].IsBroken;
        }
    }

    private void OnStateChanged()
    {
        int shards = GameManager.Instance.State.timeShards;

        if (shards > lastShardCount && player != null)
        {
            // At the player: a shard is collected by an interaction raycast, so
            // the player is the reliable spot for the sparkle (the shard itself
            // is destroyed the same frame it is collected).
            shardBurst.transform.position = player.position + Vector3.up;
            shardBurst.Play();
        }

        lastShardCount = shards;
    }

    private static Material particleMaterial;

    /// <summary>
    /// A ParticleSystemRenderer's default material uses a Built-in-RP shader,
    /// which URP renders as solid magenta - assign a URP-compatible unlit
    /// particle shader instead so the burst shows its configured colour.
    /// </summary>
    private static Material GetParticleMaterial()
    {
        if (particleMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit");
            particleMaterial = new Material(shader) { name = "VfxParticleUnlit" };
            particleMaterial.SetFloat("_Surface", 1f);
            particleMaterial.SetFloat("_Blend", 0f);
        }

        return particleMaterial;
    }

    private ParticleSystem CreateBurst(string name, Color color, int count, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetParticleMaterial();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.6f;
        main.startLifetime = 0.6f;
        main.startSpeed = 2.5f;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        return ps;
    }
}
