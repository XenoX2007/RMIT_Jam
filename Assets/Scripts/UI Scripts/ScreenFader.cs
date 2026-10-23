using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Màn đen phủ toàn màn hình để chuyển cảnh: mờ dần sang đen rồi load scene khác.
// Dùng thời gian thật (unscaled) nên vẫn chạy khi game đang dừng.
public static class ScreenFader
{
    // Tạo một lớp phủ đen (alpha = startAlpha) nằm trên mọi UI khác
    public static CanvasGroup CreateOverlay(float startAlpha, int sortingOrder = 1000)
    {
        var go = new GameObject("Screen Fader", typeof(Canvas), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)image.transform;
        rect.SetParent(go.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.GetComponent<Image>().color = Color.black;

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = startAlpha;
        group.blocksRaycasts = startAlpha > 0f;
        return group;
    }

    public static IEnumerator Fade(CanvasGroup group, float to, float time)
    {
        float from = group.alpha;
        group.blocksRaycasts = true;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            group.alpha = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
            yield return null;
        }
        group.alpha = to;
        group.blocksRaycasts = to > 0f;
    }

    // Đợi delay giây, mờ sang đen rồi load scene
    public static Coroutine FadeAndLoad(MonoBehaviour host, string scene, float fadeTime = 1f, float delay = 0f)
    {
        return host.StartCoroutine(FadeAndLoadRoutine(scene, fadeTime, delay));
    }

    static IEnumerator FadeAndLoadRoutine(string scene, float fadeTime, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        CanvasGroup group = CreateOverlay(0f);
        yield return Fade(group, 1f, fadeTime);
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }
}
