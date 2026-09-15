using UnityEngine;

public class ProceduralWalk : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Movement movement;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;

    [Header("Walk Feel")]
    [SerializeField] private float stepFrequency = 13f;
    [SerializeField] private float legTravel = 0.035f;
    [SerializeField] private float legRotation = 7f;

    [Header("Idle Return")]
    [SerializeField] private float idleReturnSpeed = 18f;

    private Vector3 leftStartPos;
    private Vector3 rightStartPos;
    private Quaternion leftStartRot;
    private Quaternion rightStartRot;

    private float walkTime;

    private void Awake()
    {
        if (leftLeg != null)
        {
            leftStartPos = leftLeg.localPosition;
            leftStartRot = leftLeg.localRotation;
        }

        if (rightLeg != null)
        {
            rightStartPos = rightLeg.localPosition;
            rightStartRot = rightLeg.localRotation;
        }
    }

    private void Update()
    {
        if (movement == null)
            return;

        if (!movement.IsMoving)
        {
            ReturnToIdle();
            return;
        }

        walkTime += Time.deltaTime * stepFrequency;

        float step = Mathf.Sin(walkTime);

        if (leftLeg != null)
        {
            leftLeg.localPosition =
                leftStartPos +
                Vector3.up * (step * legTravel);

            leftLeg.localRotation =
                leftStartRot *
                Quaternion.Euler(
                    0f,
                    0f,
                    step * legRotation
                );
        }

        if (rightLeg != null)
        {
            rightLeg.localPosition =
                rightStartPos -
                Vector3.up * (step * legTravel);

            rightLeg.localRotation =
                rightStartRot *
                Quaternion.Euler(
                    0f,
                    0f,
                    -step * legRotation
                );
        }
    }

    private void ReturnToIdle()
    {
        float t =
            1f - Mathf.Exp(-idleReturnSpeed * Time.deltaTime);

        if (leftLeg != null)
        {
            leftLeg.localPosition =
                Vector3.Lerp(
                    leftLeg.localPosition,
                    leftStartPos,
                    t
                );

            leftLeg.localRotation =
                Quaternion.Slerp(
                    leftLeg.localRotation,
                    leftStartRot,
                    t
                );
        }

        if (rightLeg != null)
        {
            rightLeg.localPosition =
                Vector3.Lerp(
                    rightLeg.localPosition,
                    rightStartPos,
                    t
                );

            rightLeg.localRotation =
                Quaternion.Slerp(
                    rightLeg.localRotation,
                    rightStartRot,
                    t
                );
        }
    }
}