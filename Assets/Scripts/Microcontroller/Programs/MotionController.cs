using RSMA.uDTP.Topics;
using UnityEngine;

public class MotionController : MonoBehaviour
{
    public float wheelBase = 0.5f; // Расстояние между колесами в метрах
    public float wheelRadius = 0.1f; // Радиус колеса в метрах
    public float maxVelDeg = 3600.0f; //Максимальная угловая скорость вращения

    private void Update()
    {
        // Получаем желаемые скорости (например, из другого модуля)
        var cmd = RSMA.uDTP.DataBroker.GetState<RobotVelocity>("MaruzTargetVelocity");

        // Расчет скоростей колес (линейная скорость V = omega * R)
        float leftVel = (cmd.linearVelocity - (cmd.angularVelocity * wheelBase / 2.0f)) / wheelRadius;
        float rightVel = (cmd.linearVelocity + (cmd.angularVelocity * wheelBase / 2.0f)) / wheelRadius;

        // Нормализация для вашего диапазона [-1, 1]
        // maxVel в вашем коде - это скорость в градусах/сек, пересчитаем
        float maxVelRad = maxVelDeg * Mathf.Deg2Rad;

        MotorInput leftData = new MotorInput { input = Mathf.Clamp(leftVel / maxVelRad, -1f, 1f) };
        MotorInput rightData = new MotorInput { input = Mathf.Clamp(rightVel / maxVelRad, -1f, 1f) };

        RSMA.uDTP.DataBroker.Publish("MaruzML", leftData);
        RSMA.uDTP.DataBroker.Publish("MaruzMR", rightData);
    }
}