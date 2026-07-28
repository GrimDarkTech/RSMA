using System;
using UnityEngine;
using RSMA.uDTP;
using RSMA.uDTP.Topics;

[RequireComponent(typeof(Rigidbody))]
public class PX4Quadrocopter : MonoBehaviour
{
    public int droneId = 0;

    [Header("Пропеллеры и точки моторов")]
    public GameObject propellerFR; // Motor 0 (CCW)
    public GameObject propellerBL; // Motor 1 (CCW)
    public GameObject propellerFL; // Motor 2 (CW)
    public GameObject propellerBR; // Motor 3 (CW)

    public Transform motorFRPoint;
    public Transform motorBLPoint;
    public Transform motorFLPoint;
    public Transform motorBRPoint;

    [Header("Физика")]
    public float maxThrustPerMotor = 12.0f;
    public float dragTorqueCoefficient = 0.02f;
    public float propMaxVelocity = 9800.0f;

    [Header("GPS (Домашняя точка)")]
    public double homeLatitude = 55.7558;
    public double homeLongitude = 37.6173;
    public float homeAltitudeMSL = 150.0f;

    public Vector3 centerOfmass = Vector3.zero;

    private Rigidbody rb;
    [SerializeField]
    private float[] motorCommands = new float[4] { 0f, 0f, 0f, 0f };

    private Vector3 lastLinearVelocity;
    private Vector3 currentAccelWorld;
    private Vector3 initialPositionWorld;

    private HILSensor sensorMsg;
    private HILGPS gpsMsg;

    private float gpsTimer = 0f;
    private const float GPS_INTERVAL = 0.1f; // 10 Гц

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
        gpsMsg = new HILGPS(); // Инициализация базовых полей GPS

        lastLinearVelocity = rb.linearVelocity;
        initialPositionWorld = transform.position;
        rb.centerOfMass = centerOfmass;
    }

    void Update()
    {
        // 1. Прием команд от PX4 с безопасной проверкой
        var actuatorsMsg = DataBroker.GetState<ActuatorInputs>($"ActuatorInputs_{droneId}");

        if ( actuatorsMsg.inputs != null && actuatorsMsg.size >= 4)
        {
            motorCommands[0] = Mathf.Clamp01(actuatorsMsg.inputs[0]); // FR
            motorCommands[1] = Mathf.Clamp01(actuatorsMsg.inputs[1]); // BL
            motorCommands[2] = Mathf.Clamp01(actuatorsMsg.inputs[2]); // FL
            motorCommands[3] = Mathf.Clamp01(actuatorsMsg.inputs[3]); // BR
        }

        // 2. Анимация пропеллеров
        RotatePropeller(propellerFR, -motorCommands[0]);
        RotatePropeller(propellerBL, -motorCommands[1]);
        RotatePropeller(propellerFL, motorCommands[2]);
        RotatePropeller(propellerBR, motorCommands[3]);

        // 3. Отправка GPS (10 Гц)
        gpsTimer += Time.deltaTime;
        if (gpsTimer >= GPS_INTERVAL)
        {
            PublishGPSData();
            gpsTimer = 0f;
        }
    }

    void FixedUpdate()
    {
        // Убран искусственный пропуск первых 35 кадров, из-за которого PX4 терял IMU на старте!

        // Расчет физического ускорения
        Vector3 currentVel = rb.linearVelocity;
        if (Time.fixedDeltaTime > 0f)
        {
            currentAccelWorld = (currentVel - lastLinearVelocity) / Time.fixedDeltaTime;
        }
        lastLinearVelocity = currentVel;

        // Применение сил моторов
        ApplyMotorForce(motorFRPoint != null ? motorFRPoint.position : transform.position, motorCommands[0], 1f);
        ApplyMotorForce(motorBLPoint != null ? motorBLPoint.position : transform.position, motorCommands[1], 1f);
        ApplyMotorForce(motorFLPoint != null ? motorFLPoint.position : transform.position, motorCommands[2], -1f);
        ApplyMotorForce(motorBRPoint != null ? motorBRPoint.position : transform.position, motorCommands[3], -1f);

        // Отправка IMU (каждый физический шаг)
        PublishIMUData();
    }

    private void ApplyMotorForce(Vector3 point, float command, float spinDirection)
    {
        float thrust = command * maxThrustPerMotor;
        rb.AddForceAtPosition(transform.up * thrust, point, ForceMode.Force);
        rb.AddTorque(transform.up * (thrust * dragTorqueCoefficient * spinDirection), ForceMode.Force);
    }

    private void RotatePropeller(GameObject prop, float direction)
    {
        if (prop != null) prop.transform.Rotate(0, 0, propMaxVelocity * direction * Time.deltaTime);
    }

    private void PublishIMUData()
    {
        long timestampUs = (long)(Time.timeAsDouble * 1_000_000.0);

        // 1. Кажущееся ускорение в локальных координатах Unity (с учетом гравитации)
        Vector3 totalAccelWorld = currentAccelWorld - Physics.gravity;
        Vector3 localAccel = transform.InverseTransformDirection(totalAccelWorld);

        // 2. Угловая скорость в локальных координатах Unity
        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);

        // 3. Магнитное поле Земли
        Vector3 magWorld = new Vector3(0.0f, -0.15f, 0.43f);
        Vector3 localMag = transform.InverseTransformDirection(magWorld);

        sensorMsg.timestamp = timestampUs;

        // --- ПЕРЕВОД В СК PX4 FRD ---
        sensorMsg.accel_x = localAccel.z;
        sensorMsg.accel_y = localAccel.x;
        sensorMsg.accel_z = -localAccel.y;

        sensorMsg.gyro_x = localAngularVel.z;
        sensorMsg.gyro_y = localAngularVel.x;
        sensorMsg.gyro_z = -localAngularVel.y;

        sensorMsg.mag_x = localMag.z;
        sensorMsg.mag_y = localMag.x;
        sensorMsg.mag_z = -localMag.y;

        // --- БАРОМЕТР И ВЫСОТА ---
        float currentAltMSL = homeAltitudeMSL + transform.position.y;
        sensorMsg.abs_pressure = 1013.25f * Mathf.Pow(1.0f - (currentAltMSL / 44330.0f), 5.255f);
        sensorMsg.pressure_alt = currentAltMSL;
        sensorMsg.temperature = 15.0f;

        DataBroker.Publish($"HILSensor_{droneId}", sensorMsg);
    }

    private void PublishGPSData()
    {
        long timestampMs = (long)(Time.timeAsDouble * 1000.0);

        Vector3 localPos = transform.position - initialPositionWorld;
        double lat = homeLatitude + (localPos.z / 111000.0);
        double lon = homeLongitude + (localPos.x / (111000.0 * Math.Cos(homeLatitude * Math.PI / 180.0)));

        Vector3 velWorld = rb.linearVelocity;

        gpsMsg.timestamp = timestampMs;
        gpsMsg.fix_type = 3; // Жизненно важно: 3 = 3D Fix (иначе EKF2 считает GPS невалидным)

        gpsMsg.lat = (int)(lat * 1e7);
        gpsMsg.lon = (int)(lon * 1e7);
        gpsMsg.alt = (int)((homeAltitudeMSL + transform.position.y) * 1000f);

        gpsMsg.eph = 100;
        gpsMsg.epv = 100;
        gpsMsg.vel = (ushort)(new Vector2(velWorld.x, velWorld.z).magnitude * 100f);

        gpsMsg.vn = (short)(velWorld.z * 100f);
        gpsMsg.ve = (short)(velWorld.x * 100f);
        gpsMsg.vd = (short)(-velWorld.y * 100f);

        float cogDeg = Mathf.Atan2(velWorld.x, velWorld.z) * Mathf.Rad2Deg;
        if (cogDeg < 0) cogDeg += 360f;
        gpsMsg.cog = (ushort)(cogDeg * 100f);

        gpsMsg.satellites_visible = 12;

        DataBroker.Publish($"HILGPS_{droneId}", gpsMsg);
    }
}