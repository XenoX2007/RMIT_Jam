using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    [SerializeField] string gameScene1 = "InGame";
    [SerializeField] string gameScene2 = "CreditsScene";

    public void PlayGame()
    {
        SceneManager.LoadScene(gameScene1);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
    public void CreditScene()
    {
        SceneManager.LoadScene(gameScene2);
    }
}
