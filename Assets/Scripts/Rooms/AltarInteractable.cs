using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class AltarInteractable : MonoBehaviour
{
    [Header("Interaktion")]
    [Tooltip("Wenn AN, wird der Altar schon beim bloßen Berühren aktiviert. Wenn AUS, erst per Tastendruck (E / Hoch / Dreieck).")]
    [SerializeField] private bool activateOnTouch = false;

    [Header("Visuelles Feedback")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color inactiveColor = new Color(0.65f, 0.75f, 0.85f, 0.95f);
    [SerializeField] private Color activeColor = new Color(0.35f, 0.98f, 1f, 1f);
    [SerializeField] private Color cooldownColor = new Color(0.55f, 0.55f, 0.65f, 0.85f);
    [SerializeField] private Color exhaustedColor = new Color(0.30f, 0.30f, 0.35f, 0.75f);
    [SerializeField] private Color corruptedColor = new Color(0.95f, 0.20f, 0.35f, 1f);

    private Vector2Int roomCoordinate;
    private AltarStateData cachedState;
    private bool playerInRange = false;
    private string lastFeedbackText = "";
    private float feedbackTimer = 0f;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    public void Initialize(Vector2Int coord, AltarStateData state)
    {
        roomCoordinate = coord;
        SyncVisualState(state);
    }

    public void SyncVisualState(AltarStateData state)
    {
        cachedState = state;
        if (spriteRenderer == null || cachedState == null) return;

        spriteRenderer.color = cachedState.status switch
        {
            AltarStatus.Inactive => inactiveColor,
            AltarStatus.Active => activeColor,
            AltarStatus.OnCooldown => cooldownColor,
            AltarStatus.Exhausted => exhaustedColor,
            AltarStatus.Corrupted => corruptedColor,
            _ => inactiveColor
        };
    }

    private void Update()
    {
        if (feedbackTimer > 0f)
        {
            feedbackTimer -= Time.deltaTime;
        }

        if (!playerInRange || activateOnTouch) return;
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.FreeRoam) return;

        bool interactPressed = false;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.eKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                interactPressed = true;
            }
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            if (gamepad.dpad.up.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame)
            {
                interactPressed = true;
            }
        }

        if (interactPressed)
        {
            TriggerInteraction();
        }
    }

    private void TriggerInteraction()
    {
        if (AltarManager.Instance == null) return;

        AltarManager.Instance.TryInteractWithAltar(roomCoordinate, out string msg);
        if (!string.IsNullOrEmpty(msg))
        {
            lastFeedbackText = msg;
            feedbackTimer = 2.2f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") && other.GetComponent<PlatformerSlimeController>() == null) return;

        playerInRange = true;
        if (activateOnTouch && cachedState != null && cachedState.status == AltarStatus.Inactive)
        {
            TriggerInteraction();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") && other.GetComponent<PlatformerSlimeController>() == null) return;
        playerInRange = false;
    }

    private void OnGUI()
    {
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.FreeRoam) return;
        if (!playerInRange && feedbackTimer <= 0f) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(transform.position + new Vector3(0f, 1.5f, 0f));
        if (screenPos.z < 0f) return;

        float guiX = screenPos.x;
        float guiY = Screen.height - screenPos.y;

        string labelText = feedbackTimer > 0f ? lastFeedbackText : GetPromptText();
        if (string.IsNullOrEmpty(labelText)) return;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.white;

        float width = Mathf.Max(180f, labelText.Length * 8.5f);
        GUI.Box(new Rect(guiX - width * 0.5f, guiY - 14f, width, 28f), labelText, style);
    }

    private string GetPromptText()
    {
        if (cachedState == null) return "";

        return cachedState.status switch
        {
            AltarStatus.Inactive => "[E / Hoch] Altar erwecken",
            AltarStatus.Active => "[E / Hoch] Altar nutzen",
            AltarStatus.OnCooldown => $"Altar lädt ({cachedState.cooldownTicksRemaining} Ticks)",
            AltarStatus.Exhausted => "Altar erschöpft",
            AltarStatus.Corrupted => "[E / Hoch] Altar reinigen",
            _ => ""
        };
    }
}