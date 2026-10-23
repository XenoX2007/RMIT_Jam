using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class InteractionController : MonoBehaviour
{
    public static BarrelInteractable CurrentBarrel;

    [SerializeField] private float interactRadius = 3f;
    [SerializeField] private LayerMask interactLayer;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private TMP_Text interactionText;

    private IInteractable currentInteractable;
    private InteractableHighlight currentHighlight;

    private void OnEnable()
    {
        interactAction.action.Enable();
    }

    private void OnDisable()
    {
        interactAction.action.Disable();
    }

    private void Update()
    {
        // Player is hiding in barrel
        if (CurrentBarrel != null)
        {
            // Remove highlight
            if (currentHighlight != null)
            {
                currentHighlight.UnHighlight();
                currentHighlight = null;
            }

            interactionText.gameObject.SetActive(true);
            interactionText.text = "Press E To Exit";

            if (interactAction.action.WasPressedThisFrame())
            {
                CurrentBarrel.ExitBarrel();
            }

            return;
        }

        FindNearestInteractable();

        if (!interactAction.action.WasPressedThisFrame())
            return;

        currentInteractable?.Interact();
    }

    private void FindNearestInteractable()
    {
        if (CurrentBarrel != null)
            return;

        // Clear old highlight
        if (currentHighlight != null)
        {
            currentHighlight.UnHighlight();
            currentHighlight = null;
        }

        currentInteractable = null;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            interactRadius,
            interactLayer);

        float closestDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            IInteractable interactable =
                hit.GetComponentInParent<IInteractable>();

            if (interactable == null)
                continue;

            float distance =
                Vector3.Distance(
                    transform.position,
                    hit.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                currentInteractable = interactable;
            }
        }

        // Prompt
        if (currentInteractable != null)
        {
            interactionText.gameObject.SetActive(true);
            interactionText.text =
                currentInteractable.GetPrompt();

            // Highlight
            currentHighlight =
                ((MonoBehaviour)currentInteractable)
                .GetComponentInChildren<InteractableHighlight>();

            if (currentHighlight != null)
            {
                currentHighlight.Highlight();
            }
        }
        else
        {
            interactionText.gameObject.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            transform.position,
            interactRadius);
    }
}