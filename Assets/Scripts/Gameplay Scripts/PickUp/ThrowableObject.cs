using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThrowableObject : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private InputActionReference throwAction;
    [SerializeField] private float throwForce = 15f;

    private Rigidbody rb;
    private Collider objectCollider;
    private PlayerThrower thrower;

    private bool isHeld;
    private bool inFlight;   // thrown and not yet hit anything

    public static ThrowableObject HeldObject;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        objectCollider = GetComponent<Collider>();

        if (holdPoint != null)
            thrower = holdPoint.GetComponentInParent<PlayerThrower>();
    }

    // Rest where placed in the scene until something touches it (no rolling off slopes at start)
    private void Start()
    {
        if (isHeld || rb == null)
            return;

        // On something floating (a boat): ride along with it until picked up
        if (transform.parent != null && transform.parent.GetComponentInParent<WaterBob>() != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            return;
        }

        if (!rb.isKinematic)
            rb.Sleep();
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
            // Animated throw when the player has a PlayerThrower, instant throw otherwise
            if (thrower != null)
                thrower.Throw(this);
            else
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

        if (!rb.isKinematic)   // coconuts resting on a boat are already kinematic
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;   // follow the hand exactly while carried

        objectCollider.enabled = false;

        transform.SetParent(holdPoint);

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Debug.Log("Picked Up");
    }

    // The throw animation starts: the item leaves the hold point and follows the hands
    public void TakeFromHolder()
    {
        isHeld = false;
        transform.SetParent(null);
    }

    // Let go at the release point of the throw animation
    public void Release(Vector3 velocity)
    {
        HeldObject = null;

        isHeld = false;

        transform.SetParent(null);

        objectCollider.enabled = true;

        // Don't bump into the thrower on the way out
        if (thrower != null && thrower.TryGetComponent(out Collider playerCollider))
            StartCoroutine(IgnorePlayerFor(playerCollider, 0.5f));

        rb.interpolation = RigidbodyInterpolation.Interpolate;   // smooth flight
        rb.isKinematic = false;
        rb.useGravity = true;

        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = velocity;

        inFlight = true;

        Debug.Log("Thrown");
    }

    // First thing hit after a throw: an enemy gets knocked back and stunned
    private void OnCollisionEnter(Collision collision)
    {
        if (!inFlight)
            return;

        inFlight = false;

        Enemy enemy = collision.collider.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.HitByThrow(enemy.transform.position - transform.position);
            Debug.Log("Hit " + enemy.name);
        }
    }

    private IEnumerator IgnorePlayerFor(Collider playerCollider, float seconds)
    {
        Physics.IgnoreCollision(objectCollider, playerCollider, true);
        yield return new WaitForSeconds(seconds);
        if (objectCollider != null && playerCollider != null)
            Physics.IgnoreCollision(objectCollider, playerCollider, false);
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

        inFlight = true;

        Debug.Log("Thrown");
    }

    public string GetPrompt()
    {
        return isHeld
            ? "Click To Throw"
            : "Press E To Pick Up";
    }
}
