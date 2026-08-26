using TMPro;
using UnityEngine;

/// <summary>
/// World-space, billboarded, progress-aware tutorial text. This is placed in
/// the scene as 3D geometry (TextMeshPro, not TextMeshProUGUI on a Canvas),
/// which is what the "instructions in 3D" clause actually asks for - a
/// screen overlay would not satisfy it no matter how dynamic its text was.
///
/// TutorialTrigger (Step 3.2) SetActive(true)s this object once, on the
/// player's first approach - that is the documented "fade in on approach".
/// From then on this component owns its own visibility: it fades out as the
/// player walks away and back in on return, rather than needing a bespoke
/// "was the verb actually performed" signal per trigger, which would mean a
/// different detector for each of the eight verbs.
///
/// The message can reference {energy} and {health}; they are substituted
/// with the player's live values every time the text becomes visible, which
/// is what makes the same static Inspector string read as dynamic rather
/// than a fixed label.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public sealed class WorldTutorialText : MonoBehaviour
{
    [SerializeField] private float fadeDistance = 6f;
    [SerializeField] private float fadeSpeed = 4f;

    private TextMeshPro label;
    private Transform player;
    private string template;
    private float alpha;
    private bool wasVisible;

    private void Awake()
    {
        label = GetComponent<TextMeshPro>();
        template = label.text;

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            player = found.transform;
        }
    }

    private void OnEnable()
    {
        alpha = 0f;
        wasVisible = false;
        ApplyColor();
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            // Billboard toward the camera so it reads in both camera modes.
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }

        bool inRange = player != null &&
            Vector3.Distance(transform.position, player.position) <= fadeDistance;

        if (inRange && !wasVisible)
        {
            ApplyTemplate();
        }

        wasVisible = inRange;

        alpha = Mathf.MoveTowards(alpha, inRange ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        ApplyColor();
    }

    private void ApplyColor()
    {
        Color c = label.color;
        c.a = alpha;
        label.color = c;
    }

    private void ApplyTemplate()
    {
        if (GameManager.Instance == null)
        {
            label.text = template;
            return;
        }

        GameState state = GameManager.Instance.State;
        int energyPercent = Mathf.RoundToInt(100f * state.currentEnergy / state.maxEnergy);
        int healthPercent = Mathf.RoundToInt(100f * state.currentHealth / state.maxHealth);

        label.text = template
            .Replace("{energy}", energyPercent + "%")
            .Replace("{health}", healthPercent + "%");
    }
}
