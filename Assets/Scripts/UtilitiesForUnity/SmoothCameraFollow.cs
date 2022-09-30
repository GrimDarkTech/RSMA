using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    private Transform targetTranform;
    private Transform cameraTransform;
    private Vector3 targetPosition;
    private Vector3 smoothedPosition;

    public float smoothValue = 0.1f;
    public GameObject target;
    public Vector3 offset;
    public bool IsFollowRotation;
    public Vector3 lockAxis;
    private void Start()
    {
        cameraTransform = gameObject.transform;
        targetTranform = target.transform;
    }

    private void FixedUpdate()
    {

        if (IsFollowRotation)
        {
            Quaternion tartgerRotation = targetTranform.rotation;
            tartgerRotation.x = tartgerRotation.x * (1-lockAxis.x);
            tartgerRotation.y = tartgerRotation.y * (1-lockAxis.y);
            tartgerRotation.z = tartgerRotation.z * (1-lockAxis.z);
            Quaternion smoothedRotation = Quaternion.Lerp(cameraTransform.rotation, tartgerRotation, smoothValue);
            cameraTransform.rotation = smoothedRotation;
            targetPosition = targetTranform.position - offset.magnitude * cameraTransform.forward;
            smoothedPosition = Vector3.Lerp(cameraTransform.position, targetPosition, smoothValue);
            cameraTransform.position = smoothedPosition;
        }
        else
        {
            targetPosition = targetTranform.position + offset;
            smoothedPosition = Vector3.Lerp(cameraTransform.position, targetPosition, smoothValue);
            cameraTransform.position = smoothedPosition;
        }
    }
}
 