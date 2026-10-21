using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MenuSlideIn : MonoBehaviour
{
    [SerializeField] Button[] menuButtons;
    [SerializeField] float startDelay = 2f;
    [SerializeField] float duration = 0.7f;
    [SerializeField] float stagger = 0.2f;
    [SerializeField] Vector2 startOffset = new Vector2(-500f, 0f);

    Vector2[] targetPositions;
    CanvasGroup[] groups;

    void Awake()
    {
        targetPositions = new Vector2[menuButtons.Length];
        groups = new CanvasGroup[menuButtons.Length];

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;

            RectTransform rect = menuButtons[i].GetComponent<RectTransform>();
            groups[i] = menuButtons[i].GetComponent<CanvasGroup>();

            if (groups[i] == null)
                groups[i] = menuButtons[i].gameObject.AddComponent<CanvasGroup>();

            targetPositions[i] = rect.anchoredPosition;

            // Ẩn nút và đặt nó ở vị trí bắt đầu ngay khi scene chạy
            rect.anchoredPosition = targetPositions[i] + startOffset;
            groups[i].alpha = 0f;
            groups[i].interactable = false;
            groups[i].blocksRaycasts = false;
        }
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(startDelay);

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;

            RectTransform rect = menuButtons[i].GetComponent<RectTransform>();
            StartCoroutine(AnimateButton(rect, groups[i], targetPositions[i]));
            yield return new WaitForSeconds(stagger);
        }
    }

    IEnumerator AnimateButton(RectTransform rect, CanvasGroup group, Vector2 target)
    {
        Vector2 start = rect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            rect.anchoredPosition = Vector2.Lerp(start, target, eased);
            group.alpha = eased;
            yield return null;
        }

        rect.anchoredPosition = target;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }
}