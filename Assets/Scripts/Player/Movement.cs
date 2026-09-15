using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Movement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 7f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    public Vector2 MoveInput => moveInput;
    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsMoving => moveInput.sqrMagnitude > 0.001f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void Update()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput * speed;
    }
}