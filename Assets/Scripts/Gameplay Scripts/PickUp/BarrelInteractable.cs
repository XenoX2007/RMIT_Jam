using UnityEngine;

public class BarrelInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject player;
    [SerializeField] private Transform hidePoint;

    private Movement movement;
    private Rigidbody playerRB;

    private bool isHidden;
    private readonly System.Collections.Generic.List<Renderer> hiddenRenderers = new System.Collections.Generic.List<Renderer>();

    private void Start()
    {
        movement = player.GetComponent<Movement>();
        playerRB = player.GetComponent<Rigidbody>();
    }

    public void Interact()
    {
        if (isHidden)
            return;

        EnterBarrel();
    }

    private void EnterBarrel()
    {
        isHidden = true;

        InteractionController.CurrentBarrel = this;

        movement.CanMove = false;

        playerRB.linearVelocity = Vector3.zero;
        playerRB.angularVelocity = Vector3.zero;

        playerRB.isKinematic = true;
        playerRB.useGravity = false;

        playerRB.position = hidePoint.position;
        player.transform.position =
            hidePoint.position;

        // Hide the character while inside (only the renderers that were visible)
        hiddenRenderers.Clear();
        foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled)
                continue;

            r.enabled = false;
            hiddenRenderers.Add(r);
        }

        Debug.Log("Hidden");
    }

    public void ExitBarrel()
    {
        isHidden = false;

        InteractionController.CurrentBarrel = null;

        movement.CanMove = true;

        // Step out beside the barrel, standing on the ground (not half buried)
        Vector3 exitPosition =
            transform.position +
            transform.right * 2f +
            Vector3.up * 1.2f;

        playerRB.position = exitPosition;
        player.transform.position = exitPosition;

        playerRB.isKinematic = false;
        playerRB.useGravity = true;

        foreach (Renderer r in hiddenRenderers)
        {
            if (r != null)
                r.enabled = true;
        }
        hiddenRenderers.Clear();

        Debug.Log("Exited");
    }

    public string GetPrompt()
    {
        return "Press E To Hide";
    }
}