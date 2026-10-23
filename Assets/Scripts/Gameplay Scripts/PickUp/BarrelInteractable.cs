using UnityEngine;

public class BarrelInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject player;
    [SerializeField] private Transform hidePoint;

    private Movement movement;
    private Rigidbody playerRB;

    private bool isHidden;

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

        player.transform.position =
            hidePoint.position;

        Debug.Log("Hidden");
    }

    public void ExitBarrel()
    {
        isHidden = false;

        InteractionController.CurrentBarrel = null;

        movement.CanMove = true;

        playerRB.isKinematic = false;
        playerRB.useGravity = true;

        player.transform.position =
            transform.position +
            transform.right * 2f;

        Debug.Log("Exited");
    }

    public string GetPrompt()
    {
        return "Press E To Hide";
    }
}