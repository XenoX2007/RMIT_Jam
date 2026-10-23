using UnityEngine;

public class ObjectivePiece : MonoBehaviour, IInteractable
{
    [Tooltip("Fog that clears when this piece is collected (e.g. the fog around its house).")]
    [SerializeField] private FogDissolve[] fogsToClear;

    public void Interact()
    {
        ObjectiveManager.Instance.CollectPiece();

        if (fogsToClear != null)
            foreach (FogDissolve fog in fogsToClear)
                if (fog != null)
                    fog.Dissolve();

        Destroy(gameObject);
    }

    public string GetPrompt()
    {
        return "Press E to Collect";
    }
}
