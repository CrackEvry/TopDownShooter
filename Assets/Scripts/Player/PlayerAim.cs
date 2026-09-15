using UnityEngine;

public class PlayerAim : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float spriteAngleOffset = 0f;

    public Vector2 AimDirection { get; private set; }

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        if (visual == null || mainCamera == null)
            return;

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        AimDirection =
            ((Vector2)mouseWorld - (Vector2)transform.position).normalized;

        if (AimDirection.sqrMagnitude < 0.001f)
            return;

        float angle =
            Mathf.Atan2(AimDirection.y, AimDirection.x) *
            Mathf.Rad2Deg;

        visual.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle + spriteAngleOffset
            );
    }
}