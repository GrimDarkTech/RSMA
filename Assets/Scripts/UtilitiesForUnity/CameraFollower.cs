using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollower : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 5, -10);
    public Vector3 rotationOffset = new Vector3(15, 0, 0);

    [Header("Smoothing")]
    public float positionSmoothTime = 0.25f;
    public float rotationSmoothSpeed = 5f;

    [Header("Focusing (On Double Click)")]
    public float focusDistance = 5f;
    public float focusDuration = 0.5f;

    private Vector3 _currentVelocity;
    private bool _isFollowing;
    private bool _isFocusing;

    private Vector3 _focusTargetPosition;
    private Quaternion _focusTargetRotation;
    private float _focusProgress;
    private Vector3 _focusStartPosition;
    private Quaternion _focusStartRotation;

    private FreeCameraPro _freeCameraComponent;

    private void Awake()
    {
        _freeCameraComponent = GetComponent<FreeCameraPro>();
    }

    private void LateUpdate()
    {
        if (_isFocusing)
        {
            UpdateFocusAnimation();
            return;
        }

        if (_isFollowing && target != null)
        {
            UpdateFollow();
        }
    }

    public void EnableFreeCamera()
    {
        _isFollowing = false;
        _isFocusing = false;

        if (_freeCameraComponent != null)
            _freeCameraComponent.enabled = true;
    }
    public void StartFollowing(Transform newTarget)
    {
        if (newTarget == null) return;

        target = newTarget;
        _isFollowing = true;
        _isFocusing = false;

        if (_freeCameraComponent != null)
            _freeCameraComponent.enabled = false;
    }

    public void FocusOnTarget(Transform newTarget)
    {
        if (newTarget == null) return;

        target = newTarget;
        _isFollowing = false;
        _isFocusing = true;

        if (_freeCameraComponent != null)
            _freeCameraComponent.enabled = false;

        _focusStartPosition = transform.position;
        _focusStartRotation = transform.rotation;
        _focusProgress = 0f;

        _focusTargetPosition = target.position - transform.forward * focusDistance;
        _focusTargetRotation = Quaternion.LookRotation(target.position - _focusTargetPosition);
    }

    private void UpdateFollow()
    {
        Vector3 targetPosition = target.position + target.rotation * offset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref _currentVelocity,
            positionSmoothTime
        );

        Quaternion targetRotation = Quaternion.LookRotation(target.position - transform.position) * Quaternion.Euler(rotationOffset);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSmoothSpeed
        );
    }

    private void UpdateFocusAnimation()
    {
        _focusProgress += Time.deltaTime / focusDuration;

        transform.position = Vector3.Lerp(_focusStartPosition, _focusTargetPosition, _focusProgress);
        transform.rotation = Quaternion.Slerp(_focusStartRotation, _focusTargetRotation, _focusProgress);

        if (_focusProgress >= 1f)
        {
            _isFocusing = false;
        }
    }
}