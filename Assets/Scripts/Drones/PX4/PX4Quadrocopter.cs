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
        gpsMsg = new HILGPS();

        lastLinearVelocity = rb.linearVelocity;
        initialPositionWorld = transform.position;
    }

    void Update()
    {
        // 1. Прием команд от PX4 (Quad X: 0:FR, 1:BL, 2:FL, 3:BR)
        var actuatorsMsg = DataBroker.GetState<ActuatorInputs>($"ActuatorInputs_{droneId}");
        if (actuatorsMsg.inputs != null && actuatorsMsg.size >= 4)
        {
            motorCommands[0] = Mathf.Clamp01(actuatorsMsg.inputs[0]);
            motorCommands[1] = Mathf.Clamp01(actuatorsMsg.inputs[1]);
            motorCommands[2] = Mathf.Clamp01(actuatorsMsg.inputs[2]);
            motorCommands[3] = Mathf.Clamp01(actuatorsMsg.inputs[3]);
        }

        // 2. Анимация пропеллеров (CCW: -1, CW: +1)
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
        // Расчет физического ускорения
        Vector3 currentVel = rb.linearVelocity;
        currentAccelWorld = (currentVel - lastLinearVelocity) / Time.fixedDeltaTime;
        lastLinearVelocity = currentVel;

        // Применение сил моторов
        // CCW пропеллер создает реактивный момент по часовой стрелке (+1)
        ApplyMotorForce(motorFRPoint != null ? motorFRPoint.position : transform.position, motorCommands[0], 1f);
        ApplyMotorForce(motorBLPoint != null ? motorBLPoint.position : transform.position, motorCommands[1], 1f);
        ApplyMotorForce(motorFLPoint != null ? motorFLPoint.position : transform.position, motorCommands[2], -1f);
        ApplyMotorForce(motorBRPoint != null ? motorBRPoint.position : transform.position, motorCommands[3], -1f);

        // Отправка IMU
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
        long timestampUs = (long)(Time.fixedTimeAsDouble * 1_000_000.0);

        // 1. Кажущееся ускорение в локальных координатах Unity
        Vector3 totalAccelWorld = currentAccelWorld - Physics.gravity;
        Vector3 localAccel = transform.InverseTransformDirection(totalAccelWorld);

        // 2. Угловая скорость в локальных координатах Unity
        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);

        // 3. Магнитное поле Земли (Север по +Z, Земля по -Y)
        Vector3 magWorld = new Vector3(0.0f, -0.4f, 0.2f);
        Vector3 localMag = transform.InverseTransformDirection(magWorld);

        // Шум для магнетометра
        float magNoiseX = UnityEngine.Random.Range(-0.001f, 0.001f);
        float magNoiseY = UnityEngine.Random.Range(-0.001f, 0.001f);
        float magNoiseZ = UnityEngine.Random.Range(-0.001f, 0.001f);

        sensorMsg.timestamp = timestampUs;

        // --- ПЕРЕВОД В СК PX4 FRD ---
        // Акселерометр: Unity (X:Right, Y:Up, Z:Forward) -> PX4 (X:Forward, Y:Right, Z:Down)
        sensorMsg.accel_x = localAccel.z;
        sensorMsg.accel_y = localAccel.x;
        sensorMsg.accel_z = -localAccel.y;

        // Гироскоп: учет правила правой руки для осей FRD
        sensorMsg.gyro_x = localAngularVel.z; // Roll Rate
        sensorMsg.gyro_y = -localAngularVel.x; // Pitch Rate
        sensorMsg.gyro_z = localAngularVel.y;  // Yaw Rate

        // Магнетометр: Unity -> PX4 FRD
        sensorMsg.mag_x = localMag.z + magNoiseZ;
        sensorMsg.mag_y = -localMag.x + magNoiseX;
        sensorMsg.mag_z = localMag.y + magNoiseY;

        // --- БАРОМЕТР И ВЫСОТА ---
        float currentAltMSL = homeAltitudeMSL + transform.position.y;

        // Международная барометрическая формула (hPa)
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

        gpsMsg.lat = (int)(lat * 1e7);
        gpsMsg.lon = (int)(lon * 1e7);
        gpsMsg.alt = (int)((homeAltitudeMSL + transform.position.y) * 1000f);

        gpsMsg.eph = 100;
        gpsMsg.epv = 100;
        gpsMsg.vel = (ushort)(new Vector2(velWorld.x, velWorld.z).magnitude * 100f);

        // Скорости в NED (см/с): +Z = North, +X = East, -Y = Down
        gpsMsg.vn = (short)(velWorld.z * 100f);  // North
        gpsMsg.ve = (short)(velWorld.x * 100f);  // East
        gpsMsg.vd = (short)(-velWorld.y * 100f); // Down

        float cogDeg = Mathf.Atan2(velWorld.x, velWorld.z) * Mathf.Rad2Deg;
        if (cogDeg < 0) cogDeg += 360f;
        gpsMsg.cog = (ushort)(cogDeg * 100f);

        gpsMsg.satellites_visible = 12;

        DataBroker.Publish($"HILGPS_{droneId}", gpsMsg);
    }
}