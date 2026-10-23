using UnityEngine;

public class InteractableHighlight : MonoBehaviour
{
    [SerializeField] private Material highlightMaterial;

    private Material originalMaterial;
    private Renderer rend;

    private void Awake()
    {
        rend = GetComponent<Renderer>();

        originalMaterial = rend.material;
    }

    public void Highlight()
    {
        rend.material = highlightMaterial;
    }

    public void UnHighlight()
    {
        rend.material = originalMaterial;
    }
}