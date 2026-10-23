using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Opening story in the style of the Shadow Fight 2 intro:
// full-screen pictures that slowly drift, a new picture slides in from the right,
// the story text types out underneath. Click / Space / Enter = next (first press finishes the typing),
// ESC = skip. After the last screen it fades to black and loads the game.
public class StoryIntro : MonoBehaviour
{
    [System.Serializable]
    public class IntroScreen
    {
        public Sprite image;                 // empty = black background
        [TextArea(3, 14)] public string text;
        public bool titleCard;               // big centred text instead of the bottom band
    }

    [SerializeField] private IntroScreen[] screens;
    [SerializeField] private string nextScene = "InGame";

    [Header("Layers")]
    [SerializeField] private Image pictureA;
    [SerializeField] private Image pictureB;
    [SerializeField] private RectTransform textBand;
    [SerializeField] private CanvasGroup textBandGroup;
    [SerializeField] private TMP_Text bandText;
    [SerializeField] private TMP_Text centerText;
    [SerializeField] private CanvasGroup blackout;   // full-screen black for fades

    [Header("Timing")]
    [SerializeField] private float slideTime = 1.2f;
    [SerializeField] private float charsPerSecond = 38f;
    [Tooltip("Seconds a screen stays after its text is fully shown (plus a little per character).")]
    [SerializeField] private float readTime = 2.2f;
    [SerializeField] private float readTimePerChar = 0.025f;
    [SerializeField] private float fadeTime = 1.2f;

    [Header("Picture drift")]
    [SerializeField] private float driftZoom = 0.06f;      // grows up to +6% while shown
    [SerializeField] private float driftDistance = 40f;    // pixels it pans
    [SerializeField] private float driftDuration = 14f;

    private Image front, back;
    private float frontShownAt;
    private Vector2 driftDir;
    private bool skipping;

    private void Start()
    {
        front = pictureA;
        back = pictureB;
        back.gameObject.SetActive(false);
        textBandGroup.alpha = 0f;
        centerText.alpha = 0f;
        blackout.alpha = 1f;
        StartCoroutine(Play());
    }

    private void Update()
    {
        // Slow zoom + pan of the picture on screen
        if (front != null && front.gameObject.activeSelf)
        {
            float k = Mathf.Clamp01((Time.time - frontShownAt) / driftDuration);
            k = k * k * (3f - 2f * k);
            front.rectTransform.localScale = Vector3.one * (1f + driftZoom * k);
            if (!sliding) front.rectTransform.anchoredPosition = driftDir * (driftDistance * k);
        }

        Keyboard kb = Keyboard.current;
        if (!skipping && kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            skipping = true;
            StopAllCoroutines();
            StartCoroutine(FinishIntro());
        }
    }

    private bool sliding;

    private IEnumerator Play()
    {
        Sprite current = null;
        for (int i = 0; i < screens.Length; i++)
        {
            IntroScreen s = screens[i];

            // Picture: first one fades in, a new one slides in from the right, same one stays
            if (i == 0)
            {
                ShowFront(s.image);
                current = s.image;
                yield return Fade(blackout, 1f, 0f, fadeTime);
            }
            else if (s.image != current)
            {
                yield return HideText();
                if (s.image == null) yield return FadePicture(front, Color.white, Color.black, fadeTime * 0.8f, false);
                else if (current == null) { ShowFront(s.image); yield return FadePicture(front, Color.black, Color.white, fadeTime * 0.8f, true); }
                else yield return SlideIn(s.image);
                current = s.image;
            }
            else
            {
                yield return HideText();
            }

            // Text
            TMP_Text label = s.titleCard || s.image == null ? centerText : bandText;
            yield return ShowText(s, label);

            // Read, or wait for a click
            float wait = readTime + s.text.Length * readTimePerChar;
            for (float t = 0f; t < wait; t += Time.deltaTime)
            {
                if (NextPressed()) break;
                yield return null;
            }
        }

        yield return FinishIntro();
    }

    private void ShowFront(Sprite sprite)
    {
        front.gameObject.SetActive(sprite != null);
        front.sprite = sprite;
        front.rectTransform.anchoredPosition = Vector2.zero;
        front.rectTransform.localScale = Vector3.one;
        frontShownAt = Time.time;
        driftDir = Random.insideUnitCircle.normalized;
    }

    private IEnumerator SlideIn(Sprite sprite)
    {
        sliding = true;
        float width = ((RectTransform)front.canvas.transform).rect.width;
        back.sprite = sprite;
        back.gameObject.SetActive(true);
        back.transform.SetAsLastSibling();
        textBand.SetAsLastSibling();
        centerText.transform.SetAsLastSibling();
        blackout.transform.SetAsLastSibling();
        back.rectTransform.localScale = Vector3.one;
        back.color = Color.white;

        Vector2 frontStart = front.rectTransform.anchoredPosition;
        for (float t = 0f; t < slideTime; t += Time.deltaTime)
        {
            float k = t / slideTime;
            k = k * k * (3f - 2f * k);
            back.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(width, 0f, k), 0f);
            // the old picture moves off more slowly and darkens (parallax)
            front.rectTransform.anchoredPosition = frontStart + new Vector2(-width * 0.35f * k, 0f);
            front.color = Color.Lerp(Color.white, new Color(0.35f, 0.35f, 0.35f), k);
            yield return null;
        }

        front.gameObject.SetActive(false);
        front.color = Color.white;
        (front, back) = (back, front);
        front.rectTransform.anchoredPosition = Vector2.zero;
        frontShownAt = Time.time;
        driftDir = Random.insideUnitCircle.normalized;
        sliding = false;
    }

    private IEnumerator ShowText(IntroScreen s, TMP_Text label)
    {
        label.text = s.text;
        label.maxVisibleCharacters = 0;
        label.alpha = 1f;
        if (label == bandText)
        {
            // Fit the dark band to the text
            label.ForceMeshUpdate();
            float h = label.GetPreferredValues(s.text, label.rectTransform.rect.width, 0f).y;
            textBand.sizeDelta = new Vector2(textBand.sizeDelta.x, h + 90f);
            yield return Fade(textBandGroup, textBandGroup.alpha, 1f, 0.35f);
        }

        int total = s.text.Length;
        float shown = 0f;
        while (shown < total)
        {
            if (NextPressed()) break;
            shown += charsPerSecond * Time.deltaTime;
            label.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        label.maxVisibleCharacters = total;
        yield return null;   // so the same press doesn't also skip the screen
    }

    private IEnumerator HideText()
    {
        float a = Mathf.Max(textBandGroup.alpha, centerText.alpha);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.3f;
            textBandGroup.alpha = Mathf.Min(textBandGroup.alpha, a * k);
            centerText.alpha = Mathf.Min(centerText.alpha, a * k);
            yield return null;
        }
        textBandGroup.alpha = 0f;
        centerText.alpha = 0f;
    }

    private IEnumerator FinishIntro()
    {
        yield return Fade(blackout, blackout.alpha, 1f, fadeTime);
        SceneManager.LoadScene(nextScene);
    }

    // Picture to / from black, with the text layer staying on top
    private static IEnumerator FadePicture(Image picture, Color from, Color to, float time, bool keepActive)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            picture.color = Color.Lerp(from, to, k * k * (3f - 2f * k));
            yield return null;
        }
        picture.color = to;
        if (!keepActive) { picture.gameObject.SetActive(false); picture.color = Color.white; }
    }

    private static IEnumerator Fade(CanvasGroup group, float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            group.alpha = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
            yield return null;
        }
        group.alpha = to;
    }

    private static bool NextPressed()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame);
    }
}
