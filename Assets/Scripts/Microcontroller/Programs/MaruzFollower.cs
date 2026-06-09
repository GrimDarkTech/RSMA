using RSMA.uDTP.Topics;
using System.Collections.Generic;
using UnityEngine;

public class MaruzFollower : MonoBehaviour
{
    [HideInInspector]
    public List<TrajectoryPoint> currentPath = new List<TrajectoryPoint>();

    [Header("Настройки движения")]
    public float lookAheadDistance = 0.4f; // Дистанция "взгляда вперед"
    public float arrivalThreshold = 0.15f; // Радиус финиша последней точки
    public float maxLinearSpeed = 1.5f;

    [Header("PID Регулятор (угловой)")]
    public float Kp = 3.0f;
    public float Ki = 0.01f;
    public float Kd = 0.2f;

    private float integral = 0;
    private float lastError = 0;
    private int targetPointIdx = 0;

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

        Vector3 robotPos = transform.position;

        // 1. Ищем точку на траектории, которая находится на расстоянии lookAheadDistance
        while (targetPointIdx < currentPath.Count - 1 &&
               Vector3.Distance(robotPos, currentPath[targetPointIdx].position) < lookAheadDistance)
        {
            targetPointIdx++;
        }

        TrajectoryPoint currentTarget = currentPath[targetPointIdx];
        float distanceToFinal = Vector3.Distance(robotPos, currentPath[currentPath.Count - 1].position);

        // Условие остановки на финише
        if (distanceToFinal < arrivalThreshold)
        {
            currentPath.Clear();
            StopRobot();
            return;
        }

        // 2. Расчет локальных координат цели
        Vector3 localTarget = transform.InverseTransformPoint(currentTarget.position);

        // Ошибка по углу в радианах [-PI, PI]
        float error = Mathf.Atan2(localTarget.x, localTarget.z);

        // 3. Вычисление PID для угловой скорости
        float deltaTime = Time.deltaTime;
        if (deltaTime > 0)
        {
            integral += error * deltaTime;
            // Ограничение интегральной суммы во избежание "разгона"
            integral = Mathf.Clamp(integral, -1.0f, 1.0f);

            float derivative = (error - lastError) / deltaTime;
            lastError = error;

            float currentAngularVel = (Kp * error) + (Ki * integral) + (Kd * derivative);

            // 4. Расчет линейной скорости
            // Замедinternal скорость, если угол ошибки слишком большой (плавное торможение на поворотах)
            float speedFactor = Mathf.Clamp01(Mathf.Cos(error));
            float currentLinearVel = currentTarget.targetVelocity * speedFactor;

            // Если это самый первый старт и цель сильно сзади — крутимся на месте
            if (targetPointIdx == 0 && Mathf.Abs(error) > 60 * Mathf.Deg2Rad)
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
}