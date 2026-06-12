using RSMA.uDTP;
using RSMA.uDTP.Topics;
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

public class MaruzFollower : MonoBehaviour
{
    public BoardOrientation sensorOrientation = BoardOrientation.None;

    public List<TrajectoryPoint> currentPath = new List<TrajectoryPoint>();

    public float lookAheadDistance = 0.4f; // Дистанция "взгляда вперед"
    public float arrivalThreshold = 0.3f; // Радиус финиша последней точки

    public float Kp = 1.0f;
    public float Ki = 0.005f;
    public float Kd = 0.1f;

    private float integral = 0;
    private float lastError = 0;
    private int targetPointIdx = 0;

    RSMA.uDTP.Topics.Pose robotPose;

    public void SetNewPath(List<TrajectoryPoint> path)
    {
        currentPath = path;
        targetPointIdx = 0;
        integral = 0;
        lastError = 0;

    }

    private void Update()
    {
        if (currentPath == null || currentPath.Count == 0 || targetPointIdx >= currentPath.Count)
        {
            StopRobot();
            return;
        }

        robotPose = DataBroker.GetState<RSMA.uDTP.Topics.Pose>("MaruzPose");

        // 1. Ищем точку на траектории, которая находится на расстоянии lookAheadDistance
        while (targetPointIdx < currentPath.Count - 1 &&
               Vector3.Distance(robotPose.position, currentPath[targetPointIdx].position) < lookAheadDistance)
        {
            targetPointIdx++;
        }

        TrajectoryPoint currentTarget = currentPath[targetPointIdx];
        float distanceToFinal = Vector3.Distance(robotPose.position, currentPath[currentPath.Count - 1].position);

        // Условие остановки на финише
        if (distanceToFinal < arrivalThreshold)
        {
            currentPath.Clear();
            StopRobot();
            return;
        }

        // 2. Расчет локальных координат цели
        Vector3 localTarget = transform.InverseTransformPoint(currentTarget.position);

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
            float currentLinearVel = currentTarget.targetVelocity * speedFactor;

            // Сравниваем градусы с градусами (errorDeg с 60 градусами)
            if (targetPointIdx == 0 && Mathf.Abs(errorDeg) > 60.0f)
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
        for (int i = 0; i < currentPath.Count; i++)
        {
            Gizmos.DrawSphere(currentPath[i].position, 0.1f);
        }

    }
}