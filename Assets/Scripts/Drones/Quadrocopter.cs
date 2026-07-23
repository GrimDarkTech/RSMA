using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Quadrocopter : MonoBehaviour
{
    public int droneId;

    [Header("Физика и ограничения")]
    public float maxSpeed = 5.0f;
    public float positionKp = 4.0f;
    public float positionKd = 2.0f;

    private Rigidbody rb;
    private Vector3 targetPosition;
    private bool isControlledByApi = false;

    private RSMA.uDTP.Topics.Pose pose;
    private RSMA.uDTP.Topics.Pose targetPose;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 2.5f; // Масса дрона из config.py (2.5 кг)
        rb.linearDamping = 0.5f;
        rb.angularDamping = 2.0f;
        rb.useGravity = true;
    }

    private void Start()
    {
        pose = new RSMA.uDTP.Topics.Pose();
        pose.position = transform.position;
        pose.rotation = transform.rotation;
        pose.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish($"DronePose_{droneId}", pose);
        targetPose.position = transform.position;
    }

    private void Update()
    {
        // 1. Отправляем текущую телеметрию в RSMA
        pose.position = transform.position;
        pose.rotation = transform.rotation;
        pose.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish($"DronePose_{droneId}", pose);

        targetPose = DataBroker.GetState<RSMA.uDTP.Topics.Pose>($"DroneTargetPose_{droneId}");

        if (targetPose.position != Vector3.zero)
        {
            SetTargetPosition(targetPose.position);
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

        // Расчет требуемого ускорения/силы для достижения targetPosition (PD-регулятор)
        Vector3 positionError = targetPosition - transform.position;
        Vector3 force = (positionError * positionKp) - (rb.linearVelocity * positionKd);

        // Добавляем компенсацию силы тяжести
        force += -Physics.gravity * rb.mass;

        // Ограничение максимального усилия
        force = Vector3.ClampMagnitude(force, 100.0f);

        rb.AddForce(force, ForceMode.Force);

        // Плавный поворот корпуса дрона по направлению движения (для визуала)
        if (positionError.sqrMagnitude > 0.01f)
        {
            Vector3 targetForward = new Vector3(positionError.x, 0, positionError.z).normalized;
            if (targetForward != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetForward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 5.0f);
            }
        }
    }

    // Телеметрия для отправки в Python
    public DroneData GetData()
    {
        return new DroneData
        {
            id = droneId,
            position = new float[] { transform.position.x, transform.position.y, transform.position.z },
            velocity = new float[] { rb.linearVelocity.x, rb.linearVelocity.y, rb.linearVelocity.z },
            rotation = new float[] { transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z }
        };
    }
}

[System.Serializable]
public struct DroneData
{
    public int id;
    public float[] position;
    public float[] velocity;
    public float[] rotation;
}