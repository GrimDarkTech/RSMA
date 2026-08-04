using System;
using UnityEngine;
using RSMA.uDTP;
using RSMA.uDTP.Topics;
using RSMA.PX4;

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

    public Vector3 propAxis = Vector3.up;

    public Transform motorFRPoint;
    public Transform motorBLPoint;
    public Transform motorFLPoint;
    public Transform motorBRPoint;

    [Header("Физика")]
    public float maxThrustPerMotor = 12.0f;
    public float dragTorqueCoefficient = 0.02f;
    public float propMaxVelocity = 9800.0f;
    public float mass = 0.5f;

    [Header("GPS (Домашняя точка)")]
    public double homeLatitude = 55.7558;
    public double homeLongitude = 37.6173;
    public float homeAltitudeMSL = 150.0f;

    [Header("Настройки шумов датчиков")]
    public float gyroNoiseFactor = 0.005f;   // Шум гироскопа (рад/с)
    public float accelNoiseFactor = 0.05f;   // Шум акселерометра (м/с^2)
    public float magNoiseFactor = 0.002f;    // Шум магнитометра (Гаусс)

    [Header("Магнитное поле")]
    public Vector3 magWorldNED = new Vector3(0.3f, 0.0f, 0.43f);

    public Vector3 centerOfMass = Vector3.zero;

    private Rigidbody rb;
    [SerializeField]
    private float[] motorCommands = new float[4] { 0f, 0f, 0f, 0f };

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
        rb.mass = mass;
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
        if (prop != null) 
        {
            Vector3 rotation = propAxis * propMaxVelocity * direction * Time.deltaTime;
            prop.transform.Rotate(rotation);
        }
    }

    private void PublishHILStateData(long timestampUs)
    {
        stateMsg.timestamp = timestampUs;

        // Поворот в NED
        Quaternion nedRot = UnityToNEDConverter.RotationToNED(transform.rotation);
        stateMsg.orientation = new float[4] { nedRot.w, nedRot.x, nedRot.y, nedRot.z };

        // Угловые скорости в NED
        Vector3 nedAngularVel = UnityToNEDConverter.AngularVelocityToNED(rb.angularVelocity);
        stateMsg.rollspeed = nedAngularVel.x;  // Roll (North axis in NED frame)
        stateMsg.pitchspeed = nedAngularVel.y; // Pitch (East axis)
        stateMsg.yawspeed = nedAngularVel.z;   // Yaw (Down axis)

        // Линейные скорости в NED
        Vector3 nedVel = UnityToNEDConverter.VelocityToNED(rb.linearVelocity);
        stateMsg.vn = (short)(nedVel.x * 100f);  // см/с
        stateMsg.ve = (short)(nedVel.y * 100f);  // см/с
        stateMsg.vd = (short)(nedVel.z * 100f);  // см/с

        // Координаты GPS
        Vector3 localPos = transform.position - initialPositionWorld;
        var (lat, lon) = UnityToNEDConverter.UnityPosToLatLon(localPos, homeLatitude, homeLongitude);

        stateMsg.lat = (int)(lat * 1e7);
        stateMsg.lon = (int)(lon * 1e7);
        stateMsg.alt = (int)((homeAltitudeMSL + transform.position.y) * 1000f);

        DataBroker.Publish($"HILStateQuaternion_{droneId}", stateMsg);
    }

    private void PublishHILSensorData(long timestampUs)
    {
        sensorMsg.timestamp = timestampUs;

        // 1. Ускорение: учитываем гравитацию и переводим в Body FRD
        Vector3 accelWorldUnity = currentAccelWorld - Physics.gravity;
        Vector3 accelBodyUnity = transform.InverseTransformDirection(accelWorldUnity);
        Vector3 accelFRD = UnityToNEDConverter.BodyUnityToBodyFRD(accelBodyUnity);

        sensorMsg.accel_x = accelFRD.x + UnityEngine.Random.Range(-accelNoiseFactor, accelNoiseFactor);
        sensorMsg.accel_y = accelFRD.y + UnityEngine.Random.Range(-accelNoiseFactor, accelNoiseFactor);
        sensorMsg.accel_z = accelFRD.z + UnityEngine.Random.Range(-accelNoiseFactor, accelNoiseFactor);

        // 2. Гироскоп: переводим локальную угловую скорость Unity Body в FRD
        Vector3 localAngularVelUnity = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 gyroFRD = UnityToNEDConverter.AngularVelocityToBodyFRD(localAngularVelUnity);

        sensorMsg.gyro_x = gyroFRD.x + UnityEngine.Random.Range(-gyroNoiseFactor, gyroNoiseFactor);
        sensorMsg.gyro_y = gyroFRD.y + UnityEngine.Random.Range(-gyroNoiseFactor, gyroNoiseFactor);
        sensorMsg.gyro_z = gyroFRD.z + UnityEngine.Random.Range(-gyroNoiseFactor, gyroNoiseFactor);

        // 3. Магнитометр: поворот мирового вектора NED в локальную систему FRD
        Quaternion nedRot = UnityToNEDConverter.RotationToNED(transform.rotation);
        Vector3 magBodyFRD = Quaternion.Inverse(nedRot) * magWorldNED;

        sensorMsg.mag_x = magBodyFRD.x + UnityEngine.Random.Range(-magNoiseFactor, magNoiseFactor);
        sensorMsg.mag_y = magBodyFRD.y + UnityEngine.Random.Range(-magNoiseFactor, magNoiseFactor);
        sensorMsg.mag_z = magBodyFRD.z + UnityEngine.Random.Range(-magNoiseFactor, magNoiseFactor);

        // 4. Барометр
        float alt = homeAltitudeMSL + transform.position.y;
        float noisyAlt = alt + UnityEngine.Random.Range(-0.5f, 0.5f);

        sensorMsg.pressure_alt = noisyAlt;
        sensorMsg.abs_pressure = 1013.25f * Mathf.Pow(1.0f - 0.0000225577f * noisyAlt, 5.25588f);
        sensorMsg.temperature = 20.0f;

        DataBroker.Publish($"HILSensor_{droneId}", sensorMsg);
    }

    private void PublishHILGPSData(long timestampUs)
    {
        gpsMsg.timestamp = timestampUs;
        gpsMsg.fix_type = 3;

        Vector3 nedPos = UnityToNEDConverter.PositionToNED(transform.position - initialPositionWorld);
        var (lat, lon) = UnityToNEDConverter.NEDToLatLon(new Vector2(nedPos.x, nedPos.y), homeLatitude, homeLongitude);
        double alt = homeAltitudeMSL - nedPos.z;

        gpsMsg.lat = (int)(lat * 1e7);
        gpsMsg.lon = (int)(lon * 1e7);
        gpsMsg.alt = (int)(alt * 1000.0); // мм MSL

        gpsMsg.eph = 100;
        gpsMsg.epv = 100;

        Vector3 nedVel = UnityToNEDConverter.VelocityToNED(rb.linearVelocity);
        gpsMsg.vn = (short)(nedVel.x * 100f); // см/с
        gpsMsg.ve = (short)(nedVel.y * 100f); // см/с
        gpsMsg.vd = (short)(nedVel.z * 100f); // см/с

        float groundSpeed = new Vector2(nedVel.x, nedVel.y).magnitude;
        gpsMsg.vel = (ushort)(groundSpeed * 100f); // см/с

        // Путевой угол (COG) в сантиградусах (0..35999)
        float headingDeg = Mathf.Atan2(nedVel.y, nedVel.x) * Mathf.Rad2Deg;
        if (headingDeg < 0) headingDeg += 360f;
        gpsMsg.cog = (ushort)(headingDeg * 100f);

        gpsMsg.satellites_visible = 12;

        DataBroker.Publish($"HILGPS_{droneId}", gpsMsg);
    }

    private void PublishHILOpticalFlowData(long timestampUs)
    {
        flowMsg.timestamp = timestampUs;
        flowMsg.time_usec = (ulong)timestampUs;
        flowMsg.quality = 255;
        flowMsg.distance = 0f;

        DataBroker.Publish($"HILOpticalFlow_{droneId}", flowMsg);
    }
}