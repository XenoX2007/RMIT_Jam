using UnityEngine;
using UnityEngine.InputSystem;

public class ThrowableObject : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private InputActionReference throwAction;
    [SerializeField] private float throwForce = 15f;

    private Rigidbody rb;
    private Collider objectCollider;

    private bool isHeld;

    public static ThrowableObject HeldObject;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        objectCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        throwAction.action.Enable();
    }

    private void OnDisable()
    {
        throwAction.action.Disable();
    }

    private void Update()
    {
        if (!isHeld)
            return;

        if (throwAction.action.WasPressedThisFrame())
        {
            Throw();
        }
    }

    public void Interact()
    {
        if (HeldObject != null)
            return;

        PickUp();
    }

    private void PickUp()
    {
        HeldObject = this;

        isHeld = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.isKinematic = true;
        rb.useGravity = false;

        objectCollider.enabled = false;

        transform.SetParent(holdPoint);

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Debug.Log("Picked Up");
    }

    private void Throw()
    {
        HeldObject = null;

        isHeld = false;

        transform.SetParent(null);

        objectCollider.enabled = true;

        rb.isKinematic = false;
        rb.useGravity = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(
            Camera.main.transform.forward * throwForce,
            ForceMode.Impulse);

        Debug.Log("Thrown");
    }

    public string GetPrompt()
    {
        return isHeld
            ? "Click To Throw"
            : "Press E To Pick Up";
    }
}