using UnityEngine;

public class PlayerAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float movingThreshold = 0.01f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (animator == null || rb == null) return;
        animator.SetBool(IsMovingHash, rb.linearVelocity.sqrMagnitude > movingThreshold);
    }
}
