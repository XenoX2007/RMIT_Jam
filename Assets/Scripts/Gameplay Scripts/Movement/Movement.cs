using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveForce = 30f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Sprint")]
    [SerializeField] private float sprintMultiplier = 1.8f;

    [Header("Camera Facing")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InputActionReference rightClickAction;
    [SerializeField] private float cameraFacingSpeed = 15f;

    [Header("Stand Up")]
    [SerializeField] private float standUpSpeed = 2f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector2 moveInput;
    public bool CanMove = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        rightClickAction.action.Enable();
        sprintAction.action.Enable();

        moveAction.action.performed += OnMove;
        moveAction.action.canceled += OnMove;

        jumpAction.action.performed += OnJump;
    }

    private void OnDisable()
    {
        moveAction.action.performed -= OnMove;
        moveAction.action.canceled -= OnMove;

        jumpAction.action.performed -= OnJump;

        moveAction.action.Disable();
        jumpAction.action.Disable();
        rightClickAction.action.Disable();
        sprintAction.action.Disable();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (!isGrounded)
            return;

        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.Impulse);
    }

    private void Update()
    {
        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer);
    }

    private void FixedUpdate()
    {
        if (!CanMove)
            return;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 input =
            cameraForward * moveInput.y +
            cameraRight * moveInput.x;

        input.Normalize();

        // Movement (hold Shift = Sprint)
        float speed = sprintAction.action.IsPressed()
            ? moveForce * sprintMultiplier
            : moveForce;

        rb.AddForce(
            input * speed,
            ForceMode.Acceleration);

        // RMB = Face Camera Direction
        if (rightClickAction.action.IsPressed())
        {
            if (cameraForward.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(cameraForward);

                rb.MoveRotation(
                    Quaternion.Slerp(
                        rb.rotation,
                        targetRotation,
                        cameraFacingSpeed * Time.fixedDeltaTime));
            }
        }
        // Normal = Face Movement Direction
        else if (input.magnitude > 0.1f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(input);

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime));
        }

        // Extra Falling Force
        if (rb.linearVelocity.y < 0)
        {
            rb.AddForce(
                Vector3.down * 20f,
                ForceMode.Acceleration);
        }

        // Stand Upright
        Quaternion uprightRotation =
            Quaternion.Euler(
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