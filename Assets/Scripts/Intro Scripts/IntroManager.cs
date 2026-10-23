using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class IntroManager : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private StoryPage[] pages;

    [Header("Current")]
    [SerializeField] private Image currentImage;
    [SerializeField] private TMP_Text currentText;

    [Header("Next")]
    [SerializeField] private RectTransform nextPage;
    [SerializeField] private Image nextImage;
    [SerializeField] private TMP_Text nextText;

    [Header("Pages")]
    [SerializeField] private RectTransform currentPage;

    private int pageIndex = 0;
    private bool turning;

    private void Start()
    {
        currentImage.sprite =
            pages[0].image;

        currentText.text =
            pages[0].storyText;
    }

    private void Update()
    {
        if (turning)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(TurnPage());
        }
    }

    private IEnumerator TurnPage()
    {
        if (pageIndex + 1 >= pages.Length)
        {
            SceneManager.LoadScene("InGame");
            yield break;
        }

        turning = true;

        nextImage.sprite =
            pages[pageIndex + 1].image;

        nextText.text =
            pages[pageIndex + 1].storyText;

        nextPage.anchoredPosition =
            new Vector2(2000, 0);

        currentPage.anchoredPosition =
            Vector2.zero;

        float timer = 0;

        while (timer < 1)
        {
            timer += Time.deltaTime * 2f;

            currentPage.anchoredPosition =
                Vector2.Lerp(
                    Vector2.zero,
                    new Vector2(-2000, 0),
                    timer);

            nextPage.anchoredPosition =
                Vector2.Lerp(
                    new Vector2(2000, 0),
                    Vector2.zero,
                    timer);

            yield return null;
        }

        pageIndex++;

        currentImage.sprite =
            pages[pageIndex].image;

        currentText.text =
            pages[pageIndex].storyText;

        currentPage.anchoredPosition =
            Vector2.zero;

        turning = false;
    }
}