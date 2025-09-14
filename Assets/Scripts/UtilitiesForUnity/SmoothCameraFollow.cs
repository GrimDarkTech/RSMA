using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    private Vector3 smoothedPosition;
    private Vector3 targetPosition;

    public GameObject target;

    public float smoothValue = 0.1f;

    public Vector3 offset;

    public bool IsFollowRotation;

    void FixedUpdate()
    {
        if (IsFollowRotation)
        {
            Quaternion tartgerRotation = target.transform.rotation;
            Quaternion smoothedRotation = Quaternion.Lerp(target.transform.rotation, tartgerRotation, smoothValue);
            gameObject.transform.rotation = smoothedRotation;

            targetPosition = target.transform.position + offset.z * gameObject.transform.forward + offset.y * gameObject.transform.up + offset.x * gameObject.transform.right;
            smoothedPosition = Vector3.Lerp(gameObject.transform.position, targetPosition, smoothValue);
        }
        else
        {
            targetPosition = target.transform.position + offset;
            smoothedPosition = Vector3.Lerp(gameObject.transform.position, targetPosition, smoothValue);
        }
        gameObject.transform.position = smoothedPosition;
    }
}
 