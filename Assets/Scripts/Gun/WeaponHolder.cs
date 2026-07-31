using UnityEngine;

public class WeaponHolder : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private Transform handPoint;
    [SerializeField] private float throwForce = 15f;

    private GameObject currentWeapon;
    private GameObject nearbyWeapon;

    private void Update()
    {
        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        if (currentWeapon != null)
        {
            Throw();
        }
        else if (nearbyWeapon != null)
        {
            PickUp(nearbyWeapon);
        }
        else
        {
            Debug.Log("No weapon in pickup range.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        DetectWeapon(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        DetectWeapon(other);
    }

    private void DetectWeapon(Collider2D other)
    {
        if (currentWeapon != null)
        {
            return;
        }

        Gun gun = other.GetComponentInParent<Gun>();

        if (gun == null)
        {
            return;
        }

        GameObject weapon = gun.gameObject;

        if (!weapon.CompareTag("Weapon"))
        {
            return;
        }

        nearbyWeapon = weapon;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Gun gun = other.GetComponentInParent<Gun>();

        if (gun == null)
        {
            return;
        }

        if (gun.gameObject == nearbyWeapon)
        {
            nearbyWeapon = null;
        }
    }

    private void PickUp(GameObject weapon)
    {
        if (handPoint == null)
        {
            Debug.LogError("HandPoint has not been assigned.", gameObject);
            return;
        }

        currentWeapon = weapon;
        nearbyWeapon = null;

        Rigidbody2D rb = weapon.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        foreach (Collider2D weaponCollider
                 in weapon.GetComponentsInChildren<Collider2D>())
        {
            weaponCollider.enabled = false;
        }

        weapon.transform.SetParent(handPoint, true);
        weapon.transform.position = handPoint.position;
        weapon.transform.rotation = handPoint.rotation;

        Gun gunScript = weapon.GetComponent<Gun>();

        if (gunScript != null)
        {
            gunScript.enabled = true;
        }
    }

    private void Throw()
    {
        GameObject weapon = currentWeapon;

        currentWeapon = null;
        nearbyWeapon = null;

        weapon.transform.SetParent(null, true);

        Gun gunScript = weapon.GetComponent<Gun>();

        if (gunScript != null)
        {
            gunScript.enabled = false;
        }

        foreach (Collider2D weaponCollider
                 in weapon.GetComponentsInChildren<Collider2D>())
        {
            weaponCollider.enabled = true;
        }

        Rigidbody2D rb = weapon.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError(
                $"{weapon.name} does not have a Rigidbody2D.",
                weapon
            );

            return;
        }

        rb.simulated = true;

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("No camera with the MainCamera tag exists.");
            return;
        }

        Vector3 mouseWorldPosition =
            mainCamera.ScreenToWorldPoint(Input.mousePosition);

        mouseWorldPosition.z = transform.position.z;

        Vector2 throwDirection =
            (mouseWorldPosition - transform.position).normalized;

        rb.linearVelocity = throwDirection * throwForce;
    }
}