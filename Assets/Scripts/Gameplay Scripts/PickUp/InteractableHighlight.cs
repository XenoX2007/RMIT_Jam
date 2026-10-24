using UnityEngine;

public class InteractableHighlight : MonoBehaviour
{
    [SerializeField] private Material highlightMaterial;

    // Every visible mesh of the object (its own renderer and/or a model placed as a child)
    private Renderer[] renderers;
    private Material[][] originalMaterials;

    private void Awake()
    {
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer))
                list.Add(r);
        renderers = list.ToArray();

        originalMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].materials;
    }

    public void Highlight()
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = highlightMaterial;
            r.materials = mats;
        }
    }

    public void UnHighlight()
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].materials = originalMaterials[i];
    }
}
