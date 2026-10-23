using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum ArrowDirection { Left, Down, Up, Right }

// Một nốt nhạc chạy trên thanh dưới màn hình. RhythmGameManager di chuyển và chấm điểm nó.
public class RhythmNote : MonoBehaviour
{
    [SerializeField] Image arrowImage;
    [SerializeField] Color leftColor = new Color(0.76f, 0.29f, 0.6f);
    [SerializeField] Color downColor = new Color(0f, 0.9f, 1f);
    [SerializeField] Color upColor = new Color(0.07f, 0.98f, 0.02f);
    [SerializeField] Color rightColor = new Color(0.98f, 0.22f, 0.25f);
    [SerializeField] Color missColor = new Color(0.35f, 0.35f, 0.35f);

    public ArrowDirection Direction { get; private set; }
    public double HitTime { get; private set; }
    public RectTransform Rect { get; private set; }
    public Color NoteColor => ColorFor(Direction);

    public void Setup(ArrowDirection direction, double hitTime)
    {
        Direction = direction;
        HitTime = hitTime;
        Rect = (RectTransform)transform;

        // Sprite mũi tên hướng lên, xoay để ra 4 hướng
        arrowImage.color = ColorFor(direction);
        arrowImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, AngleFor(direction));
    }

    public void Hit()
    {
        StartCoroutine(FadeOut(1.6f, 0.15f));
    }

    public void Miss()
    {
        arrowImage.color = missColor;
        StartCoroutine(FadeOut(0.7f, 0.3f));
    }

    Color ColorFor(ArrowDirection direction)
    {
        switch (direction)
        {
            case ArrowDirection.Left: return leftColor;
            case ArrowDirection.Down: return downColor;
            case ArrowDirection.Up: return upColor;
            default: return rightColor;
        }
    }

    static float AngleFor(ArrowDirection direction)
    {
        switch (direction)
        {
            case ArrowDirection.Left: return 90f;
            case ArrowDirection.Down: return 180f;
            case ArrowDirection.Up: return 0f;
            default: return -90f;
        }
    }

    IEnumerator FadeOut(float targetScale, float duration)
    {
        Color startColor = arrowImage.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            transform.localScale = Vector3.one * Mathf.Lerp(1f, targetScale, eased);
            arrowImage.color = new Color(startColor.r, startColor.g, startColor.b, 1f - eased);
            yield return null;
        }

        Destroy(gameObject);
    }
}
