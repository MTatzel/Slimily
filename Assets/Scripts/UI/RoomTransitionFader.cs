using System.Collections;
using UnityEngine;

public enum RoomTransitionStyle
{
    FadeToBlack,    // Klassischer Hollow-Knight-Blackscreen
    DirectionalWipe // Schwarzer Vorhang, der in Laufrichtung wischt (Celeste-Stil)
}

public class RoomTransitionFader : MonoBehaviour
{
    private static RoomTransitionFader instance;
    public static RoomTransitionFader Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<RoomTransitionFader>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("RoomTransitionFader");
                    instance = obj.AddComponent<RoomTransitionFader>();
                }
            }
            return instance;
        }
    }

    [Header("Übergangs-Stil (Live im Inspector umschaltbar)")]
    public RoomTransitionStyle transitionStyle = RoomTransitionStyle.FadeToBlack;
    public Color overlayColor = Color.black;

    [Header("Timing (in Sekunden)")]
    [Tooltip("Dauer des Ausblendens beim Verlassen des Raums (Hollow Knight: ~0.10s).")]
    [Range(0.02f, 0.5f)] public float fadeOutDuration = 0.10f;

    [Tooltip("Kurze Pause im komplett schwarzen Bild, während Raum & Kamera umspringen.")]
    [Range(0f, 0.25f)] public float holdBlackDuration = 0.04f;

    [Tooltip("Dauer des Einblendens im neuen Raum (Hollow Knight: ~0.12s).")]
    [Range(0.02f, 0.5f)] public float fadeInDuration = 0.12f;

    private static Texture2D pixelTex;
    private float currentProgress = 0f; // 0 = unsichtbar, 1 = komplett schwarz
    private bool isWipingOut = true;    // true = Vorhang fährt zu, false = Vorhang fährt wieder auf
    private Direction currentDirection = Direction.East;

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) { Destroy(this); return; }

        EnsureTexture();
    }

    public IEnumerator FadeOutRoutine(Direction exitDirection)
    {
        currentDirection = exitDirection;
        isWipingOut = true;

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeOutDuration));
            yield return null;
        }

        currentProgress = 1f;

        if (holdBlackDuration > 0.001f)
        {
            yield return new WaitForSecondsRealtime(holdBlackDuration);
        }
    }

    public IEnumerator FadeInRoutine(Direction exitDirection)
    {
        currentDirection = exitDirection;
        isWipingOut = false;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentProgress = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(elapsed / fadeInDuration));
            yield return null;
        }

        currentProgress = 0f;
    }

    private void OnGUI()
    {
        if (currentProgress <= 0.001f) return;

        EnsureTexture();
        Color prevColor = GUI.color;
        GUI.depth = -1000; // Liegt über allen anderen OnGUI-Elementen

        if (transitionStyle == RoomTransitionStyle.FadeToBlack)
        {
            GUI.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, currentProgress);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), pixelTex);
        }
        else
        {
            DrawDirectionalWipe();
        }

        GUI.color = prevColor;
    }

    private void DrawDirectionalWipe()
    {
        GUI.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 1f);

        float w = Screen.width;
        float h = Screen.height;
        Rect wipeRect = new Rect(0f, 0f, w, h);

        // Beim Schließen (isWipingOut) wächst der Balken von 0 auf 100%.
        // Beim Öffnen (!isWipingOut) zieht er sich zur gegenüberliegenden Seite wieder raus.
        if (currentDirection == Direction.East)
        {
            float barW = w * currentProgress;
            wipeRect = isWipingOut ? new Rect(w - barW, 0f, barW, h) : new Rect(0f, 0f, barW, h);
        }
        else if (currentDirection == Direction.West)
        {
            float barW = w * currentProgress;
            wipeRect = isWipingOut ? new Rect(0f, 0f, barW, h) : new Rect(w - barW, 0f, barW, h);
        }
        else if (currentDirection == Direction.North)
        {
            float barH = h * currentProgress;
            wipeRect = isWipingOut ? new Rect(0f, 0f, w, barH) : new Rect(0f, h - barH, w, barH);
        }
        else // Direction.South
        {
            float barH = h * currentProgress;
            wipeRect = isWipingOut ? new Rect(0f, h - barH, w, barH) : new Rect(0f, 0f, w, barH);
        }

        GUI.DrawTexture(wipeRect, pixelTex);
    }

    private static void EnsureTexture()
    {
        if (pixelTex != null) return;
        pixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixelTex.SetPixel(0, 0, Color.white);
        pixelTex.Apply();
    }
}