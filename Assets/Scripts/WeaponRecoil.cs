using UnityEngine;

public class WeaponRecoil : MonoBehaviour
{
    [SerializeField] private Transform weaponVisual;
    [SerializeField] private float kickDistance = 0.06f;
    [SerializeField] private float returnSpeed = 24f;

    private Vector3 startLocalPosition;

    private void Awake()
    {
        if (weaponVisual != null)
            startLocalPosition = weaponVisual.localPosition;
    }

    private void Update()
    {
        if (weaponVisual == null)
            return;

        weaponVisual.localPosition = Vector3.Lerp(
            weaponVisual.localPosition,
            startLocalPosition,
            1f - Mathf.Exp(-returnSpeed * Time.deltaTime)
        );
    }

    public void Kick()
    {
        if (weaponVisual == null)
            return;

        weaponVisual.localPosition = startLocalPosition - Vector3.right * kickDistance;
    }
}
