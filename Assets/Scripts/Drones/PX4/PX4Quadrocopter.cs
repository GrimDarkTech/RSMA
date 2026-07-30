using System;
using UnityEngine;
using RSMA.uDTP;
using RSMA.uDTP.Topics;

[RequireComponent(typeof(Rigidbody))]
public class PX4Quadrocopter : MonoBehaviour
{
    public int droneId = 0;

    [Header("Топики (Публикация)")]
    public bool publishHILState = true;
    public bool publishHILSensor = true; // Критически важно для PX4 EKF2
    public bool publishHIL_GPS = false;
    public bool publishHIL_OpticalFlow = false;

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

    [Header("Настройки шумов датчиков")]
    public float gyroNoiseFactor = 0.005f;   // Шум гироскопа (рад/с)
    public float accelNoiseFactor = 0.05f;   // Шум акселерометра (м/с^2)
    public float magNoiseFactor = 0.002f;    // Шум магнитометра (Гаусс)

    public Vector3 centerOfMass = Vector3.zero;

    private Rigidbody rb;
    [SerializeField]
    private float[] motorCommands = new float[4] { 0f, 0f, 0f, 0f };

    private Vector3 magNED = new Vector3(0.3f, 0.0f, 0.43f);

    private Vector3 lastLinearVelocity;
    private Vector3 currentAccelWorld;
    private Vector3 initialPositionWorld;

    private HILStateQuaternion stateMsg;
    private HILSensor sensorMsg;
    private HILGPS gpsMsg;
    private HILOpticalFlow flowMsg;

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
        stateMsg = new HILStateQuaternion();
        stateMsg.orientation = new float[4] { 1f, 0f, 0f, 0f };

        sensorMsg = new HILSensor();
        gpsMsg = new HILGPS();
        flowMsg = new HILOpticalFlow();

        lastLinearVelocity = rb.linearVelocity;
        initialPositionWorld = transform.position;
        rb.centerOfMass = centerOfMass;
    }

    void Update()
    {
        // 1. Прием команд от PX4
        var actuatorsMsg = DataBroker.GetState<ActuatorInputs>($"ActuatorInputs_{droneId}");

        if (actuatorsMsg.inputs != null && actuatorsMsg.size >= 4)
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
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 currentVel = rb.linearVelocity;

        if (dt > 0f)
        {
            currentAccelWorld = (currentVel - lastLinearVelocity) / dt;
        }
        lastLinearVelocity = currentVel;

        // ИСПРАВЛЕНО: motorFLPoint проверяется корректно
        ApplyMotorForce(motorFRPoint != null ? motorFRPoint.position : transform.position, motorCommands[0], 1f);
        ApplyMotorForce(motorBLPoint != null ? motorBLPoint.position : transform.position, motorCommands[1], 1f);
        ApplyMotorForce(motorFLPoint != null ? motorFLPoint.position : transform.position, motorCommands[2], -1f);
        ApplyMotorForce(motorBRPoint != null ? motorBRPoint.position : transform.position, motorCommands[3], -1f);

        long timestampUs = (long)(Time.timeAsDouble * 1_000_000.0);

        if (publishHILState) PublishHILStateData(timestampUs);
        if (publishHILSensor) PublishHILSensorData(timestampUs);
        if (publishHIL_GPS) PublishHILGPSData(timestampUs);
        if (publishHIL_OpticalFlow) PublishHILOpticalFlowData(timestampUs);
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

    private void PublishHILStateData(long timestampUs)
    {
        stateMsg.timestamp = timestampUs;

        Quaternion rot = transform.rotation;
        // Кватернион для перехода Unity -> NED
        stateMsg.orientation = new float[4] { rot.w, rot.z, rot.x, -rot.y };

        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);

        // ИСПРАВЛЕНИЕ: инвертируем rollspeed для соответствия правосторонней системе PX4
        stateMsg.rollspeed = -localAngularVel.z;
        stateMsg.yawspeed = -localAngularVel.y;
        stateMsg.pitchspeed = localAngularVel.x;

        Vector3 velWorld = rb.linearVelocity;
        stateMsg.vn = (short)(velWorld.z * 100f);
        stateMsg.ve = (short)(velWorld.x * 100f);
        stateMsg.vd = (short)(-velWorld.y * 100f);

        Vector3 localPos = transform.position - initialPositionWorld;
        double lat = homeLatitude + (localPos.z / 111000.0);
        double lon = homeLongitude + (localPos.x / (111000.0 * Math.Cos(homeLatitude * Math.PI / 180.0)));

        stateMsg.lat = (int)(lat * 1e7);
        stateMsg.lon = (int)(lon * 1e7);
        stateMsg.alt = (int)((homeAltitudeMSL + transform.position.y) * 1000f);

        DataBroker.Publish($"HILStateQuaternion_{droneId}", stateMsg);
    }

    private void PublishHILSensorData(long timestampUs)
    {
        sensorMsg.timestamp = timestampUs;

        // --- Акселерометр ---
        Vector3 gravityNED = new Vector3(0, 0, -9.81f);
        Vector3 accelNED = new Vector3(currentAccelWorld.z, currentAccelWorld.x, -currentAccelWorld.y);

        Quaternion rotUnity = transform.rotation;
        Quaternion rotNED = new Quaternion(-rotUnity.z, rotUnity.x, -rotUnity.y, rotUnity.w);

        Vector3 localAccel = Quaternion.Inverse(rotNED) * (accelNED + gravityNED);

        // Добавляем случайный шум (равномерный или гауссовский)
        sensorMsg.accel_x = localAccel.x + UnityEngine.Random.Range(-accelNoiseFactor, accelNoiseFactor);
        sensorMsg.accel_y = localAccel.y + UnityEngine.Random.Range(-accelNoiseFactor, accelNoiseFactor);
        sensorMsg.accel_z = localAccel.z + UnityEngine.Random.Range(-accelNoiseFactor, accelNoiseFactor);

        // --- Гироскоп ---
        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);

        sensorMsg.gyro_x = localAngularVel.z + UnityEngine.Random.Range(-gyroNoiseFactor, gyroNoiseFactor);
        sensorMsg.gyro_y = -localAngularVel.x + UnityEngine.Random.Range(-gyroNoiseFactor, gyroNoiseFactor);
        sensorMsg.gyro_z = -localAngularVel.y + UnityEngine.Random.Range(-gyroNoiseFactor, gyroNoiseFactor);

        // --- Магнитометр ---
        Vector3 localMag = Quaternion.Inverse(rotNED) * magNED;

        sensorMsg.mag_x = localMag.x + UnityEngine.Random.Range(-magNoiseFactor, magNoiseFactor);
        sensorMsg.mag_y = localMag.y + UnityEngine.Random.Range(-magNoiseFactor, magNoiseFactor);
        sensorMsg.mag_z = localMag.z + UnityEngine.Random.Range(-magNoiseFactor, magNoiseFactor);

        // --- Барометр ---
        float alt = homeAltitudeMSL + transform.position.y;
        // Можно добавить небольшой шум и для высоты/давления (например, ±0.5 метра)
        float baroNoise = UnityEngine.Random.Range(-0.5f, 0.5f);
        float noisyAlt = alt + baroNoise;

        sensorMsg.pressure_alt = noisyAlt;
        sensorMsg.abs_pressure = 1013.25f * Mathf.Pow(1.0f - 0.0000225577f * noisyAlt, 5.25588f);
        sensorMsg.temperature = 20.0f;

        DataBroker.Publish($"HILSensor_{droneId}", sensorMsg);
    }

    private void PublishHILGPSData(long timestampUs)
    {
        gpsMsg.timestamp = timestampUs;
        gpsMsg.fix_type = 3; // 3D Fix

        Vector3 localPos = transform.position - initialPositionWorld;
        double lat = homeLatitude + (localPos.z / 111000.0);
        double lon = homeLongitude + (localPos.x / (111000.0 * Math.Cos(homeLatitude * Math.PI / 180.0)));

        gpsMsg.lat = (int)(lat * 1e7);
        gpsMsg.lon = (int)(lon * 1e7);
        gpsMsg.alt = (int)((homeAltitudeMSL + transform.position.y) * 1000f); // в мм

        gpsMsg.eph = 100;
        gpsMsg.epv = 100;

        Vector3 velWorld = rb.linearVelocity;
        gpsMsg.vn = (short)(velWorld.z * 100f);
        gpsMsg.ve = (short)(velWorld.x * 100f);
        gpsMsg.vd = (short)(-velWorld.y * 100f);

        float groundSpeed = new Vector2(gpsMsg.vn, gpsMsg.ve).magnitude;
        gpsMsg.vel = (ushort)groundSpeed;

        float heading = Mathf.Atan2(velWorld.x, velWorld.z) * Mathf.Rad2Deg;
        if (heading < 0) heading += 360f;
        gpsMsg.cog = (ushort)(heading * 100f);

        gpsMsg.satellites_visible = 12;

        DataBroker.Publish($"HILGPS_{droneId}", gpsMsg);
    }

    private void PublishHILOpticalFlowData(long timestampUs)
    {
        flowMsg.timestamp = timestampUs;
        flowMsg.time_usec = (ulong)timestampUs;
        flowMsg.quality = 255;
        // Здесь можно добавить raycast вниз для расчета optical flow
        flowMsg.distance = 0f;

        DataBroker.Publish($"HILOpticalFlow_{droneId}", flowMsg);
    }
}