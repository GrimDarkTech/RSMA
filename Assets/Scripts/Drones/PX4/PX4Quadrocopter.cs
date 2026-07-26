using System;
using UnityEngine;
using RSMA.uDTP;
using RSMA.uDTP.Topics;

[RequireComponent(typeof(Rigidbody))]
public class PX4Quadrocopter : MonoBehaviour
{
    public int droneId = 0;

    [Header("Пропеллеры")]
    public GameObject propellerFR; // Front Right (CCW)
    public GameObject propellerBL; // Back Left   (CCW)
    public GameObject propellerFL; // Front Left  (CW)
    public GameObject propellerBR; // Back Right  (CW)

    [Header("Точки приложения силы моторов (Transform)")]
    public Transform motorFRPoint;
    public Transform motorBLPoint;
    public Transform motorFLPoint;
    public Transform motorBRPoint;

    [Header("Параметры физики винтов")]
    [Tooltip("Максимальная тяга одного мотора в Ньютонах (при PWM = 1.0)")]
    public float maxThrustPerMotor = 12.0f; // 4 * 12 Н = 48 Н total

    [Tooltip("Коэффициент реактивного крутящего момента винта")]
    public float dragTorqueCoefficient = 0.02f;

    [Tooltip("Максимальные обороты пропеллера в град/сек (для визуализации)")]
    public float propMaxVelocity = 9800.0f;

    [Header("Настройки GPS (Домашняя точка)")]
    public double homeLatitude = 55.7558;  // Широта старта (град)
    public double homeLongitude = 37.6173; // Долгота старта (град)
    public float homeAltitudeMSL = 150.0f;  // Высота над уровнем моря (м)

    private Rigidbody rb;

    // Входные сигналы от PX4 (нормированные 0.0 ... 1.0)
    [SerializeField]
    private float[] motorCommands = new float[4] { 0f, 0f, 0f, 0f };

    // Состояния для расчета IMU и GPS
    private Vector3 lastLinearVelocity;
    private Vector3 currentAccelWorld;
    private Vector3 initialPositionWorld;

    // Буферы uDTP топиков
    private HILSensor sensorMsg;
    private HILGPS gpsMsg;

    // Таймеры публикации
    private float gpsTimer = 0f;
    private const float GPS_INTERVAL = 0.1f; // GPS отправляется с частотой 10 Гц

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 2.5f;

        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.2f;

        rb.useGravity = true;
    }

    void Start()
    {
        sensorMsg = new HILSensor();
        gpsMsg = new HILGPS();

        lastLinearVelocity = rb.linearVelocity;
        initialPositionWorld = transform.position;
    }

    void Update()
    {
        // 1. Принимаем управляющие сигналы от PX4 через новый топик ActuatorInputs
        var actuatorsMsg = DataBroker.GetState<ActuatorInputs>($"ActuatorInputs_{droneId}");
        if (actuatorsMsg.inputs != null && actuatorsMsg.size >= 4)
        {
            motorCommands[0] = Mathf.Clamp01(actuatorsMsg.inputs[0]);
            motorCommands[1] = Mathf.Clamp01(actuatorsMsg.inputs[1]);
            motorCommands[2] = Mathf.Clamp01(actuatorsMsg.inputs[2]);
            motorCommands[3] = Mathf.Clamp01(actuatorsMsg.inputs[3]);
        }

        // 2. Анимация вращения пропеллеров
        RotatePropeller(propellerFR, -motorCommands[0]);
        RotatePropeller(propellerBL, -motorCommands[1]);
        RotatePropeller(propellerFL, motorCommands[2]);
        RotatePropeller(propellerBR, motorCommands[3]);

        // 3. Публикация IMU (в кадре Update/FixedUpdate)
        PublishIMUData();

        // 4. Публикация GPS с частотой 10 Гц
        gpsTimer += Time.deltaTime;
        if (gpsTimer >= GPS_INTERVAL)
        {
            PublishGPSData();
            gpsTimer = 0f;
        }
    }

    void FixedUpdate()
    {
        // Расчет численного ускорения для акселерометрa IMU
        Vector3 currentVel = rb.linearVelocity;
        currentAccelWorld = (currentVel - lastLinearVelocity) / Time.fixedDeltaTime;
        lastLinearVelocity = currentVel;

        // Применение физических сил от 4-х моторов
        ApplyMotorForce(motorFRPoint != null ? motorFRPoint.position : transform.position, motorCommands[0], -1f);
        ApplyMotorForce(motorBLPoint != null ? motorBLPoint.position : transform.position, motorCommands[1], -1f);
        ApplyMotorForce(motorFLPoint != null ? motorFLPoint.position : transform.position, motorCommands[2], 1f);
        ApplyMotorForce(motorBRPoint != null ? motorBRPoint.position : transform.position, motorCommands[3], 1f);
    }

    private void ApplyMotorForce(Vector3 point, float command, float spinDirection)
    {
        float thrust = command * maxThrustPerMotor;
        Vector3 forceVector = transform.up * thrust;
        rb.AddForceAtPosition(forceVector, point, ForceMode.Force);

        float torque = thrust * dragTorqueCoefficient * spinDirection;
        rb.AddTorque(transform.up * torque, ForceMode.Force);
    }

    private void RotatePropeller(GameObject prop, float direction)
    {
        if (prop == null) return;
        float speed = propMaxVelocity * direction * Time.deltaTime;
        prop.transform.Rotate(0, 0, speed);
    }

    private void PublishIMUData()
    {
        long nowTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // --- 1. Линейное ускорение (включая реакцию опоры / гравитацию) ---
        Vector3 totalAccelWorld = currentAccelWorld - Physics.gravity;
        Vector3 localAccel = transform.InverseTransformDirection(totalAccelWorld);

        // --- 2. Угловая скорость (Гироскоп) ---
        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);

        // --- 3. Конвертация осей Unity (Left-Handed Y-Up) -> PX4 Body Frame (FRD: X-Fwd, Y-Right, Z-Down) ---
        sensorMsg.timestamp = nowTimestamp;

        sensorMsg.accel_x = localAccel.z;
        sensorMsg.accel_y = localAccel.x;
        sensorMsg.accel_z = -localAccel.y;

        sensorMsg.gyro_x = localAngularVel.z;
        sensorMsg.gyro_y = localAngularVel.x;
        sensorMsg.gyro_z = -localAngularVel.y;

        // --- 4. Базовый симулированный барометр и магнетометр ---
        sensorMsg.abs_pressure = 1013.25f - (transform.position.y * 0.12f); // Упрощенный градиент давления
        sensorMsg.pressure_alt = homeAltitudeMSL + transform.position.y;
        sensorMsg.temperature = 15.0f;

        // Простой вектор магнитного поля Земли в FRD
        Vector3 magWorld = new Vector3(0.2f, 0.0f, 0.4f); // Север + Вниз
        Vector3 localMag = transform.InverseTransformDirection(magWorld);
        sensorMsg.mag_x = localMag.z;
        sensorMsg.mag_y = localMag.x;
        sensorMsg.mag_z = -localMag.y;

        DataBroker.Publish($"HILSensor_{droneId}", sensorMsg);
    }

    private void PublishGPSData()
    {
        long nowTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Смещение от начальной точки спавна в метрах (перевод Unity -> NED)
        Vector3 localPos = transform.position - initialPositionWorld;
        float northOffsetMeters = localPos.z;
        float eastOffsetMeters = localPos.x;

        // Конвертация метров смещения в широту/долготу (приблизительно 1 град ~ 111,000 м)
        double lat = homeLatitude + (northOffsetMeters / 111000.0);
        double lon = homeLongitude + (eastOffsetMeters / (111000.0 * Math.Cos(homeLatitude * Math.PI / 180.0)));

        // Скорости в NED (см/с)
        Vector3 velWorld = rb.linearVelocity;
        short vn = (short)(velWorld.z * 100f);
        short ve = (short)(velWorld.x * 100f);
        short vd = (short)(-velWorld.y * 100f);

        gpsMsg.timestamp = nowTimestamp;
        gpsMsg.lat = (int)(lat * 1e7);
        gpsMsg.lon = (int)(lon * 1e7);
        gpsMsg.alt = (int)((homeAltitudeMSL + transform.position.y) * 1000f); // мм

        gpsMsg.eph = 100; // HDOP * 100 (1.0 = отлично)
        gpsMsg.epv = 100; // VDOP * 100
        gpsMsg.vel = (ushort)(new Vector2(velWorld.x, velWorld.z).magnitude * 100f);

        gpsMsg.vn = vn;
        gpsMsg.ve = ve;
        gpsMsg.vd = vd;

        // Расчет курса (Course over ground)
        float cogDeg = Mathf.Atan2(velWorld.x, velWorld.z) * Mathf.Rad2Deg;
        if (cogDeg < 0) cogDeg += 360f;
        gpsMsg.cog = (ushort)(cogDeg * 100f);

        gpsMsg.satellites_visible = 12;

        DataBroker.Publish($"HILGPS_{droneId}", gpsMsg);
    }
}