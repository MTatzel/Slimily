using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnBannerUI : MonoBehaviour
{
    public static TurnBannerUI Instance { get; private set; }

    [Header("UI-Komponenten")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI bannerText;
    [SerializeField] private Image bannerImage;

    [Header("Design-Farben")]
    [SerializeField] private Color enemyTurnColor = new Color(0.9f, 0.25f, 0.2f);
    [SerializeField] private Color playerTurnColor = new Color(0.2f, 0.8f, 0.4f);

    private Coroutine activeBannerRoutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        // Beim Start unsichtbar schalten
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Blendet das Banner mit Text und Farbe für eine bestimmte Dauer ein.
    /// </summary>
    public IEnumerator ShowBannerRoutine(string message, bool isEnemyTurn, float duration = 1.0f)
    {
        if (bannerText != null)
        {
            bannerText.text = message;
            bannerText.color = isEnemyTurn ? enemyTurnColor : playerTurnColor;
        }

        if (bannerImage != null)
        {
            // Tönt die spätere Grafik dezent ein (oder weiß lassen falls schon coloriert)
            bannerImage.color = isEnemyTurn ? enemyTurnColor : playerTurnColor;
        }

        // Einblenden (0.2s)
        yield return StartCoroutine(FadeCanvasGroup(0f, 1f, 0.2f));

        // Halten
        yield return new WaitForSeconds(duration);

        // Ausblenden (0.2s)
        yield return StartCoroutine(FadeCanvasGroup(1f, 0f, 0.2f));
    }

    private IEnumerator FadeCanvasGroup(float from, float to, float time)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / time);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}