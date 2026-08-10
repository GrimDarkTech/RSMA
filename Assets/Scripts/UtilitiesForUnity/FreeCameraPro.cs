using UnityEngine;

public class FreeCameraPro : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float boostMultiplier = 3f;
    [SerializeField] private float smoothTime = 0.1f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float rotationSmoothTime = 0.1f;
    [SerializeField] private float minVerticalAngle = -89f;
    [SerializeField] private float maxVerticalAngle = 89f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 0.5f;
    [SerializeField] private float maxZoom = 100f;

    [Header("Input")]
    [SerializeField] private KeyCode boostKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode focusKey = KeyCode.F;
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    [Header("References")]
    [SerializeField] private Transform target = null;

    [SerializeField] private float mouseThreshold = 0.01f;

    // Состояние
    private Vector3 velocity = Vector3.zero;
    private Vector3 rotationVelocity = Vector3.zero;
    private Vector3 targetPosition;
    private Vector3 targetEulerAngles;
    private bool isDragging = false;

    private void Start()
    {
        targetPosition = transform.position;
        targetEulerAngles = transform.eulerAngles;

        // Настройка курсора
        //Cursor.lockState = CursorLockMode.Confined;
    }

    private void Update()
    {
        if (Input.GetKeyDown(focusKey) && target != null)
        {
            FocusOnTarget();
        }

        if (Input.GetKeyDown(resetKey))
        {
            ResetCamera();
        }

        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    private void HandleMovement()
    {
        // WASD + Q/E движение
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) moveDirection += transform.forward;
        if (Input.GetKey(KeyCode.S)) moveDirection -= transform.forward;
        if (Input.GetKey(KeyCode.A)) moveDirection -= transform.right;
        if (Input.GetKey(KeyCode.D)) moveDirection += transform.right;
        if (Input.GetKey(KeyCode.Q)) moveDirection -= transform.up;
        if (Input.GetKey(KeyCode.E)) moveDirection += transform.up;

        if (moveDirection != Vector3.zero)
        {
            float speed = moveSpeed * (Input.GetKey(boostKey) ? boostMultiplier : 1f);
            targetPosition += moveDirection.normalized * speed * Time.deltaTime;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );
    }

    private void HandleRotation()
    {
        // Alt + ЛКМ или ПКМ для вращения
        if (Input.GetMouseButton(1) || (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButton(0)))
        {
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");

            if (Mathf.Abs(mouseX) < mouseThreshold) mouseX = 0f;
            if (Mathf.Abs(mouseY) < mouseThreshold) mouseY = 0f;

            if (mouseX != 0 || mouseY != 0)
            {
                targetEulerAngles.y += mouseX * rotationSpeed * Time.deltaTime;
                targetEulerAngles.x = Mathf.Clamp(
                    targetEulerAngles.x - mouseY * rotationSpeed * Time.deltaTime,
                    minVerticalAngle,
                    maxVerticalAngle
                );
            }
        }

        // Плавное вращение
        Quaternion targetRotation = Quaternion.Euler(targetEulerAngles);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime * 10f  // подбери множитель
        ); 
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            Vector3 zoom = transform.forward * scroll * zoomSpeed;
            Vector3 newPos = targetPosition + zoom;

            if (target != null)
            {
                float distance = Vector3.Distance(newPos, target.position);
                if (distance >= minZoom && distance <= maxZoom)
                {
                    targetPosition = newPos;
                }
            }
            else
            {
                targetPosition = newPos;
            }
        }
    }

    private void FocusOnTarget()
    {
        if (target == null) return;

        // Перемещаем камеру к цели
        Vector3 direction = (transform.position - target.position).normalized;
        float distance = Mathf.Clamp(
            Vector3.Distance(transform.position, target.position),
            minZoom,
            maxZoom
        );

        targetPosition = target.position + direction * distance;

        // Смотрим на цель
        Vector3 lookDirection = (target.position - transform.position).normalized;
        targetEulerAngles = Quaternion.LookRotation(lookDirection).eulerAngles;
    }

    private void ResetCamera()
    {
        targetPosition = new Vector3(0, 5, -10);
        targetEulerAngles = new Vector3(20, 0, 0);
    }

    // Методы для внешнего управления

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        targetPosition = position;
        targetEulerAngles = rotation.eulerAngles;
        transform.SetPositionAndRotation(position, rotation);
    }
}