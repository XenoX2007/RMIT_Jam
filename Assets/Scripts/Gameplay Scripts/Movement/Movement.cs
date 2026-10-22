using UnityEngine;


public class Movement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveForce = 30f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Stand Up")]
    [SerializeField] private float standUpSpeed = 2f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody rb;
    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse);

        }
    }

    private void FixedUpdate()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(h, 0f, v).normalized;

        // Movement
        rb.AddForce(
            input * moveForce,
            ForceMode.Acceleration);

        // Turn towards movement direction
        if (input.magnitude > 0.1f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(input);

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime));
        }
        if (rb.linearVelocity.y < 0)
        {
            rb.AddForce(Vector3.down * 20f, ForceMode.Acceleration);
        }

        // Slowly return upright
        Quaternion uprightRotation = Quaternion.Euler(
            0f,
            rb.rotation.eulerAngles.y,
            0f);

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                uprightRotation,
                standUpSpeed * Time.fixedDeltaTime));
    }
}