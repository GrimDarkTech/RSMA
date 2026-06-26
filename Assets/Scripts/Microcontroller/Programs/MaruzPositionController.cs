using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum BoardOrientation
{
    None = 0,
    Yaw45 = 45,
    Yaw90 = 90,
    Yaw135 = 135,
    Yaw180 = 180,
    Yaw225 = 225,
    Yaw270 = 270,
    Yaw315 = 315
}

public class MaruzPositionController : MonoBehaviour
{
    public BoardOrientation sensorOrientation = BoardOrientation.None;

    public TrajectoryPoint targetPoint;

    public float lookAheadDistance = 0.4f; // Дистанция "взгляда вперед"
    public float arrivalThreshold = 0.3f; // Радиус финиша последней точки

    public float Kp = 1.0f;
    public float Ki = 0.005f;
    public float Kd = 0.1f;

    private float integral = 0;
    private float lastError = 0;

    RSMA.uDTP.Topics.Pose robotPose;

    private void Start()
    {
        robotPose = DataBroker.GetState<RSMA.uDTP.Topics.Pose>("MaruzPose");

        targetPoint.position = robotPose.position;
        targetPoint.targetVelocity = 0;
        targetPoint.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish("MaruzTargetPoint", targetPoint);
    }

    private void Update()
    {
        robotPose = DataBroker.GetState<RSMA.uDTP.Topics.Pose>("MaruzPose");
        targetPoint = DataBroker.GetState<TrajectoryPoint>("MaruzTargetPoint");

        float distanceToFinal = Vector3.Distance(robotPose.position, targetPoint.position);

        // Условие остановки на финише
        if (distanceToFinal < arrivalThreshold)
        {
            StopRobot();
            return;
        }

        // 2. Расчет локальных координат цели
        Vector3 localTarget = transform.InverseTransformPoint(targetPoint.position);

        // Оставляем чистый Atan2 в радианах для тригонометрии
        float rawErrorDeg = -Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        // 2. Сдвигаем его на угол поворота полетника с автоматическим удержанием в рамках окружности
        float errorDeg = Mathf.DeltaAngle((float)sensorOrientation, rawErrorDeg);
        float errorRad = Mathf.Deg2Rad * errorDeg;

        // 3. Вычисление PID для угловой скорости (работаем с градусами)
        float deltaTime = Time.deltaTime;
        if (deltaTime > 0)
        {
            integral += errorDeg * deltaTime;
            integral = Mathf.Clamp(integral, -10.0f, 10.0f); // Чуть увеличим лимит для градусов

            float derivative = (errorDeg - lastError) / deltaTime;
            lastError = errorDeg;

            float currentAngularVel = (Kp * errorDeg) + (Ki * integral) + (Kd * derivative);

            // 4. Расчет линейной скорости
            // Передаем в Cos РАДИАНЫ (errorRad), как он и требует
            float speedFactor = Mathf.Clamp01(Mathf.Cos(errorRad));
            float currentLinearVel = targetPoint.targetVelocity * speedFactor;

            // Сравниваем градусы с градусами (errorDeg с 60 градусами)
            if (Mathf.Abs(errorDeg) > 60.0f)
            {
                currentLinearVel = 0;
            }

            // Отправка в DataBroker
            RSMA.uDTP.DataBroker.Publish("MaruzTargetVelocity", new RobotVelocity
            {
                linearVelocity = currentLinearVel,
                angularVelocity = currentAngularVel
            });
        }
    }

    private void StopRobot()
    {
        RSMA.uDTP.DataBroker.Publish("MaruzTargetVelocity", new RobotVelocity { linearVelocity = 0, angularVelocity = 0 });
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawSphere(targetPoint.position, 0.1f);
    }
}