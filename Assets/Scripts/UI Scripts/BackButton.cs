using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButton : MonoBehaviour
{
    [SerializeField] string backtoMainMenu = "MainMenu";

    public void MainMenu()
    {
        SceneManager.LoadScene(backtoMainMenu);
    }
}
