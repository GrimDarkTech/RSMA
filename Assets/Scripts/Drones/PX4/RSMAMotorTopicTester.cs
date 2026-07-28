using RSMA.uDTP.Topics; // Подключаем ваше пространство имен с топиком
using RSMA.uDTP;
using UnityEngine;

public class RSMAMotorTopicTester : MonoBehaviour
{
    [Header("Идентификатор дрона")]
    public int droneId = 0;

    [Header("ТЕСТОВЫЕ СЛАЙДЕРЫ МОТОРОВ (0.0 ... 1.0)")]
    [Tooltip("Motor 1: Front Right (Передний Правый)")]
    [Range(0f, 1f)] public float motor1_FR = 0f;

    [Tooltip("Motor 2: Back Left (Задний Левый)")]
    [Range(0f, 1f)] public float motor2_BL = 0f;

    [Tooltip("Motor 3: Front Left (Передний Левый)")]
    [Range(0f, 1f)] public float motor3_FL = 0f;

    [Tooltip("Motor 4: Back Right (Задний Правый)")]
    [Range(0f, 1f)] public float motor4_BR = 0f;

    [Header("Управление публикацией")]
    [Tooltip("Включите для отправки данных в топик")]
    public bool isPublishing = true;

    private ActuatorInputs actuatorsMsg;

    private void Awake()
    {
        // Инициализируем структуру ActuatorInputs
        actuatorsMsg = new ActuatorInputs();
        actuatorsMsg.size = 4;
        actuatorsMsg.inputs = new float[4];
    }

    private void FixedUpdate()
    {
        if (!isPublishing) return;

        // Временная метка в микросекундах (us)
        actuatorsMsg.timestamp = (long)(Time.fixedTimeAsDouble * 1_000_000.0);

        // Передаем значения слайдеров в массив
        actuatorsMsg.inputs[0] = motor1_FR;
        actuatorsMsg.inputs[1] = motor2_BL;
        actuatorsMsg.inputs[2] = motor3_FL;
        actuatorsMsg.inputs[3] = motor4_BR;

        // Публикуем в топик RSMA
        DataBroker.Publish($"ActuatorInputs_{droneId}", actuatorsMsg);
    }

    private void OnDisable()
    {
        // Обнуляем сигналы моторов при остановке или выключении компонента
        if (actuatorsMsg.inputs != null)
        {
            for (int i = 0; i < 4; i++) actuatorsMsg.inputs[i] = 0f;
            DataBroker.Publish($"ActuatorInputs_{droneId}", actuatorsMsg);
        }
    }
}