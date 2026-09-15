using UnityEngine;

public class LowerBodyDirection : MonoBehaviour
{
    [SerializeField] private Movement movement;
    [SerializeField] private Transform lowerBody;
    [SerializeField] private float spriteAngleOffset = 0f;

    private void Update()
    {
        if (movement == null || lowerBody == null)
            return;

        Vector2 direction = movement.MoveInput;

        if (direction.sqrMagnitude < 0.001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        lowerBody.rotation = Quaternion.Euler(0f, 0f, angle + spriteAngleOffset);
    }
}
