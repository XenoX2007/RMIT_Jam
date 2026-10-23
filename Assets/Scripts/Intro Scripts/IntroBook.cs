using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Intro dạng cuốn sách: các trang (Screen 1, 2, ...) xếp thành một hàng ngang,
// mỗi lần lật trang cả hàng cuộn sang phải để trang kế tiếp trượt vào giữa.
// Trang ở giữa to và rõ, các trang hai bên nhỏ và mờ hơn.
// Click / Space / Enter = trang tiếp (lần bấm đầu hiện hết chữ), ESC = bỏ qua.
// Hết trang cuối thì tối dần rồi vào game.
public class IntroBook : MonoBehaviour
{
    [SerializeField] private RectTransform strip;
    [SerializeField] private RectTransform[] pages;
    [SerializeField] private TMP_Text[] pageTexts;
    [SerializeField] private CanvasGroup blackout;
    [SerializeField] private string nextScene = "InGame";

    [Header("Timing")]
    [SerializeField] private float turnTime = 1f;
    [SerializeField] private float charsPerSecond = 40f;
    [Tooltip("Seconds a page stays after its text is fully shown (plus a little per character).")]
    [SerializeField] private float readTime = 2.2f;
    [SerializeField] private float readTimePerChar = 0.025f;
    [SerializeField] private float fadeTime = 1.2f;

    [Header("Side pages")]
    [SerializeField] private float sideScale = 0.86f;
    [SerializeField] private float sideAlpha = 0.45f;

    private CanvasGroup[] groups;
    private float spacing = 1f;
    private bool skipping;

    private void Start()
    {
        groups = new CanvasGroup[pages.Length];
        for (int i = 0; i < pages.Length; i++)
        {
            groups[i] = pages[i].GetComponent<CanvasGroup>();
            if (groups[i] == null) groups[i] = pages[i].gameObject.AddComponent<CanvasGroup>();
            if (pageTexts[i] != null) pageTexts[i].maxVisibleCharacters = 0;
        }
        if (pages.Length > 1) spacing = Mathf.Abs(pages[1].anchoredPosition.x - pages[0].anchoredPosition.x);

        strip.anchoredPosition = new Vector2(-pages[0].anchoredPosition.x, strip.anchoredPosition.y);
        blackout.alpha = 1f;
        StartCoroutine(Play());
    }

    private void Update()
    {
        UpdateFocus();

        Keyboard kb = Keyboard.current;
        if (!skipping && kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            skipping = true;
            StopAllCoroutines();
            StartCoroutine(Finish());
        }
    }

    // The page nearest the middle is big and clear, the others shrink and fade
    private void UpdateFocus()
    {
        for (int i = 0; i < pages.Length; i++)
        {
            float d = Mathf.Abs(pages[i].anchoredPosition.x + strip.anchoredPosition.x) / spacing;
            float k = Mathf.Clamp01(d);
            k = k * k * (3f - 2f * k);
            pages[i].localScale = Vector3.one * Mathf.Lerp(1f, sideScale, k);
            groups[i].alpha = Mathf.Lerp(1f, sideAlpha, k);
        }
    }

    private IEnumerator Play()
    {
        yield return Fade(1f, 0f, fadeTime);

        for (int i = 0; i < pages.Length; i++)
        {
            if (i > 0) yield return TurnTo(i);
            yield return TypeText(pageTexts[i]);

            string text = pageTexts[i] != null ? pageTexts[i].text : "";
            float wait = readTime + text.Length * readTimePerChar;
            for (float t = 0f; t < wait; t += Time.deltaTime)
            {
                if (NextPressed()) break;
                yield return null;
            }
        }

        yield return Finish();
    }

    private IEnumerator TurnTo(int index)
    {
        float from = strip.anchoredPosition.x;
        float to = -pages[index].anchoredPosition.x;
        for (float t = 0f; t < turnTime; t += Time.deltaTime)
        {
            float k = t / turnTime;
            k = k * k * (3f - 2f * k);
            strip.anchoredPosition = new Vector2(Mathf.Lerp(from, to, k), strip.anchoredPosition.y);
            yield return null;
        }
        strip.anchoredPosition = new Vector2(to, strip.anchoredPosition.y);
    }

    private IEnumerator TypeText(TMP_Text label)
    {
        if (label == null) yield break;
        label.ForceMeshUpdate();
        int total = label.textInfo.characterCount;
        float shown = 0f;
        while (shown < total)
        {
            if (NextPressed()) break;
            shown += charsPerSecond * Time.deltaTime;
            label.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        label.maxVisibleCharacters = 99999;
        yield return null;   // the same press shouldn't also turn the page
    }

    private IEnumerator Finish()
    {
        yield return Fade(blackout.alpha, 1f, fadeTime);
        SceneManager.LoadScene(nextScene);
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            blackout.alpha = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
            yield return null;
        }
        blackout.alpha = to;
    }

    private static bool NextPressed()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame);
    }
}
