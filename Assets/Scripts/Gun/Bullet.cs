using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float bulletLife = 2f;
    [SerializeField] private float bulletDamage = 1f;

    private Rigidbody2D rb;
    private Collider2D bulletCollider;
    private bool hasHit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bulletCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        rb.linearVelocity = (Vector2)transform.right * bulletSpeed;

        Destroy(gameObject, bulletLife);
    }

    public void IgnoreOwner(GameObject owner)
    {
        if (owner == null)
        {
            return;
        }

        Collider2D[] ownerColliders =
            owner.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D ownerCollider in ownerColliders)
        {
            Physics2D.IgnoreCollision(
                bulletCollider,
                ownerCollider
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Hit(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Hit(collision.gameObject);
    }

    private void Hit(GameObject hitObject)
    {
        if (hasHit)
        {
            return;
        }

        hasHit = true;

        Debug.Log(
            $"{name} raakte {hitObject.name} " +
            $"voor {bulletDamage} damage.",
            hitObject
        );

        // Voeg hier later je damage-systeem toe.
        // Bijvoorbeeld:
        //
        // EnemyHealth health =
        //     hitObject.GetComponentInParent<EnemyHealth>();
        //
        // if (health != null)
        // {
        //     health.TakeDamage(bulletDamage);
        // }

        Destroy(gameObject);
    }
}