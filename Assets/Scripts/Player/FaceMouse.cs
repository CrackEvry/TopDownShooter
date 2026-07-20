using UnityEngine;

public class FaceMouse : MonoBehaviour
{
    public Camera mainCamera;
    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        Vector3 mouseScreenPosition = Input.mousePosition;
        Vector3 mouseworldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        mouseworldPosition.z = transform.position.z;
        Vector3 direction = mouseworldPosition - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}
