using UnityEngine;

public class ObjectivePiece : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        ObjectiveManager.Instance.CollectPiece();

        Destroy(gameObject);
    }

    public string GetPrompt()
    {
        return "Press E to Collect";
    }
}