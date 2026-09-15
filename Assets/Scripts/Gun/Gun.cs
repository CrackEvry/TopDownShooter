using UnityEngine;

public class Gun : MonoBehaviour
{
    public GameObject bullet;
    public Transform firePoint;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Fire();
        }
    }

    public void Fire()
    {
        GameObject bulletObject = Instantiate(
            bullet,
            firePoint.position,
            firePoint.rotation
        );

        Bullet bulletScript = bulletObject.GetComponent<Bullet>();

        if (bulletScript != null)
        {
            bulletScript.IgnoreOwner(transform.root.gameObject);
        }
    }
}