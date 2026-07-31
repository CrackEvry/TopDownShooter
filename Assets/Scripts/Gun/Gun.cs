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
        if (firePoint == null)
        {
            Debug.LogError(
                $"FirePoint ontbreekt op gun: {gameObject.name}",
                gameObject
            );

            return;
        }

        if (bullet == null)
        {
            Debug.LogError(
                $"Bullet ontbreekt op gun: {gameObject.name}",
                gameObject
            );

            return;
        }

        Instantiate(bullet, firePoint.position, firePoint.rotation);
    }
}