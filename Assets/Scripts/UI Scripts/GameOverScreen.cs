using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Màn hình thua: ẩn lúc đầu, khi người chơi hết máu thì mờ dần hiện lên rồi dừng game.
// Nút Restart chơi lại màn, nút Main Menu về menu chính.
[RequireComponent(typeof(CanvasGroup))]
public class GameOverScreen : MonoBehaviour
{
    [SerializeField] private float fadeTime = 1.5f;
    [Tooltip("Seconds after dying before the panel starts to fade in.")]
    [SerializeField] private float delay = 0.6f;
    [SerializeField] private string mainMenuScene = "MainMenu";

    public static bool IsShowing { get; private set; }

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        IsShowing = false;
    }

    private void OnDestroy()
    {
        IsShowing = false;
    }

    // PlayerHealth -> On Died calls this
    public void Show()
    {
        if (IsShowing) return;
        IsShowing = true;
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return new WaitForSecondsRealtime(delay);
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeTime;
            group.alpha = k * k * (3f - 2f * k);
            yield return null;
        }
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        Time.timeScale = 0f;   // freeze the world behind the panel
    }

    public void Restart()
    {
        Leave(SceneManager.GetActiveScene().name);
    }

    public void MainMenu()
    {
        Leave(mainMenuScene);
    }

    private void Leave(string scene)
    {
        Time.timeScale = 1f;
        IsShowing = false;
        // Statics survive scene loads
        InteractionController.CurrentBarrel = null;
        ThrowableObject.HeldObject = null;
        SceneManager.LoadScene(scene);
    }
}
