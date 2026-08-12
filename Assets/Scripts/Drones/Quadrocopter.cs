using RSMA.uDTP;
using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Quadrocopter : MonoBehaviour
{
    public int droneId;

    [Header("Физика и PID-регулятор")]
    public float maxSpeed = 8.0f;
    public float maxForce = 250.0f;
    public float positionKp = 8.0f;
    public float positionKi = 2.0f;
    public float positionKd = 2.0f;

    public GameObject propeller1 = null;
    public GameObject propeller2 = null;
    public GameObject propeller3 = null;
    public GameObject propeller4 = null;

    private float propMaxVelocity = 9800.0f;

    private Rigidbody rb;
    private Vector3 targetPosition;
    private bool isControlledByApi = false;

    private Vector3 integralError = new Vector3(0, 0, 0);
    

    private RSMA.uDTP.Topics.Pose pose;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 2.5f; // Масса дрона из config.py (2.5 кг)

#if UNITY_2023_1_OR_NEWER
        rb.linearDamping = 0.8f;
        rb.angularDamping = 3.0f;
#else
        rb.drag = 0.8f;
        rb.angularDrag = 3.0f;
#endif
        rb.useGravity = true;
    }

    private void Start()
    {
        pose = new RSMA.uDTP.Topics.Pose();
        pose.position = transform.position;
        pose.rotation = transform.rotation;
        pose.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        targetPosition = transform.position;
        DataBroker.Publish($"DronePose_{droneId}", pose);
    }

    private void Update()
    {
        // 1. Публикуем текущую телеметрию в RSMA Broker
        pose.position = transform.position;
        pose.rotation = transform.rotation;
        pose.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish($"DronePose_{droneId}", pose);

        // 2. Безопасное получение целевой позиции из Python
        var targetPoseMsg = DataBroker.GetState<RSMA.uDTP.Topics.Pose>($"DroneTargetPose_{droneId}");

        if (targetPoseMsg.position != null)
        {
            // Убеждаемся, что координаты не нулевые
            if (targetPoseMsg.position != Vector3.zero)
            {
                SetTargetPosition(targetPoseMsg.position);
            }
        }
    }

    public void SetTargetPosition(Vector3 targetPos)
    {
        targetPosition = targetPos;
        isControlledByApi = true;
    }

    void FixedUpdate()
    {
        if (!isControlledByApi) return;

        // Расчет ошибки позиции
        Vector3 positionError = targetPosition - transform.position;
        integralError += positionError * Time.fixedDeltaTime;

        Vector3 currentVel = rb.linearVelocity;


        Vector3 force = (positionError * positionKp) + (integralError * positionKi) - (currentVel * positionKd);

        // Полная компенсация гравитации дрона
        force += -Physics.gravity * rb.mass;

        // Запас по вертикальной силе (до 250 Н на дрон), чтобы тянуть кабель и груз
        force = Vector3.ClampMagnitude(force, maxForce);

        rb.AddForce(force, ForceMode.Force);

        // Плавный поворот корпуса дрона по направлению движения (Visual Only)
        Vector3 horizontalError = new Vector3(positionError.x, 0, positionError.z);
        if (horizontalError.sqrMagnitude > 0.05f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalError.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 5.0f);
        }

        float propVelocity = propMaxVelocity * (force.magnitude / maxForce);

        propeller1.transform.Rotate(new Vector3(0, 0, -propVelocity) * Time.fixedDeltaTime);
        propeller2.transform.Rotate(new Vector3(0, 0, propVelocity) * Time.fixedDeltaTime);
        propeller3.transform.Rotate(new Vector3(0, 0, propVelocity) * Time.fixedDeltaTime);
        propeller4.transform.Rotate(new Vector3(0, 0, -propVelocity) * Time.fixedDeltaTime);
    }
}