using TMPro;
using UnityEngine;

public class ObjectiveManager : MonoBehaviour
{
    [SerializeField] private TMP_Text objectiveText;

    public static ObjectiveManager Instance;

    private int collectedPieces;

    private void Awake()
    {
        Instance = this;
    }

    public void CollectPiece()
    {
        collectedPieces++;

        Debug.Log(
            $"Collected {collectedPieces}/3");

        if (collectedPieces >= 3)
        {
            WinGame();
        }
    }

    private void WinGame()
    {
        Debug.Log("YOU WIN");
    }
    private void UpdateObjectiveText()
    {
        objectiveText.text = $"Collected {collectedPieces}/3";
    }
    private void Update()
    {
        UpdateObjectiveText();
    }
}