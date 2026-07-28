using System;
using UnityEngine;
using RSMA.uDTP;
using RSMA.uDTP.Topics;

public class DroneInputController : MonoBehaviour
{
    [Header("Drone Settings")]
    public int droneId = 0;

    [Header("Flight Mode")]
    [Tooltip("Выберите режим полета для отправки в PX4")]
    public PX4FlightMode targetFlightMode = PX4FlightMode.Position;

    [Header("Arming Control")]
    [Tooltip("Включите галочку, чтобы отправить команду ARM (запустить моторы)")]
    public bool armVehicle = false;
    private bool previousArmState = false;

    // Официальные кастомные режимы PX4 для Multirotor
    public enum PX4FlightMode
    {
        Manual = 1,
        Altitude = 3,
        Position = 4,
        Offboard = 6,
        Hold = 3,      // Loiter/Hold обычно идет на базе Altitude/Position с нулевыми стиками
        RTL = 5        // Return to Launch
    }

    void Update()
    {
        SendRCChannels();
        HandleFlightModeAndArming();
    }

    private void SendRCChannels()
    {
        RCChannelsInput rcMsg = new RCChannelsInput();
        rcMsg.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        rcMsg.chancount = 8; // Увеличиваем количество каналов до 8
        rcMsg.channels = new ushort[8];

        // 1. Крен (Правый стик - горизонталь)
        float roll = Input.GetAxis("Horizontal");
        // 2. Тангаж (Правый стик - вертикаль)
        float pitch = Input.GetAxis("Vertical");
        // 3. Газ (Левый стик - вертикаль)
        float throttle = Input.GetAxis("Throttle");
        // 4. Рыскание (Левый стик - горизонталь)
        float yaw = Input.GetAxis("Yaw");

        rcMsg.channels[0] = (ushort)Mathf.Lerp(1000, 2000, (roll + 1f) / 2f);
        rcMsg.channels[1] = (ushort)Mathf.Lerp(1000, 2000, (pitch + 1f) / 2f);
        rcMsg.channels[2] = (ushort)Mathf.Lerp(1000, 2000, (throttle + 1f) / 2f);
        rcMsg.channels[3] = (ushort)Mathf.Lerp(1000, 2000, (yaw + 1f) / 2f);

        // 5. Канал режима полета (например, тумблер: 1000 = Stabilized, 1500 = Altitude, 2000 = Position)
        // Можно привязать к значению твоего enum в инспекторе или выставить фиксировано
        rcMsg.channels[4] = targetFlightMode == PX4FlightMode.Position ? (ushort)2000 : (ushort)1000;

        // 6. Канал Арма (1000 = Disarmed, 2000 = Armed)
        rcMsg.channels[5] = armVehicle ? (ushort)2000 : (ushort)1000;

        // Остальные каналы (7 и 8) по умолчанию в нейтраль
        rcMsg.channels[6] = 1500;
        rcMsg.channels[7] = 1500;

        DataBroker.Publish($"RCChannels_{droneId}", rcMsg);
    }

    private void HandleFlightModeAndArming()
    {
        // Отправляем команду смены режима по нажатию кнопки (например, клавиша M или кнопка на геймпаде)
        if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.JoystickButton3)) // Y на Xbox / Triangle на PS
        {
            FlightModeCommand modeMsg = new FlightModeCommand();
            modeMsg.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            modeMsg.mode = (int)targetFlightMode;
            modeMsg.sub_mode = 0;

            DataBroker.Publish($"FlightMode_{droneId}", modeMsg);
            Debug.Log($"[INPUT] Запрос смены режима на: {targetFlightMode} (ID: {modeMsg.mode})");
        }

        // Отслеживаем изменение состояния ARM в инспекторе (или по кнопке 'A')
        if (armVehicle != previousArmState || Input.GetKeyDown(KeyCode.JoystickButton7)) // Start на геймпаде
        {
            if (Input.GetKeyDown(KeyCode.JoystickButton7))
            {
                armVehicle = !armVehicle; // Инвертируем по кнопке с геймпада
            }

            previousArmState = armVehicle;

            // Публикуем команду арминга/дизарминга через топик (или можно добавить отдельный структуру, если нужно)
            // Здесь мы используем тот же FlightModeCommand или передаем расширенную команду, 
            // но в PX4 арминг обрабатывается через MAV_CMD_COMPONENT_ARM_DISARM. 
            // Для простоты интеграции вынесем это в отдельную структуру ArmCommand:

            ArmCommand armMsg = new ArmCommand();
            armMsg.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            armMsg.arm = armVehicle ? (byte)1 : (byte)0;

            DataBroker.Publish($"ArmCommand_{droneId}", armMsg);
            Debug.Log($"[INPUT] Команда ARM изменена: {armVehicle}");
        }
    }
}