using System;
using System.Globalization;
using System.IO;
using System.Text;
using RSMA.PX4;
using RSMA.uDTP;
using RSMA.uDTP.Topics;
using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class VirtualSensorTest : MonoBehaviour
{
    [Header("Датчики стенда")]
    [SerializeField] private PX4Quadrocopter drone = null;
    [SerializeField] private RangeFinder rangeFinder = null;
    [SerializeField] private RSMACamera cameraPublisher = null;

    [Header("Отчёт")]
    [SerializeField, Min(1f)] private float reportIntervalSeconds = 5f;
    [SerializeField] private bool writeCsv = true;

    private const float AxisTolerance = 0.0001f;
    private const float AccelerationMargin = 0.02f;
    private const float GyroMargin = 0.002f;
    private const float MagneticMargin = 0.001f;
    private const float RangeMargin = 0.005f;
    private const float MinimumCourseSpeedMps = 0.2f;
    private const float BarometerAltitudeTolerance = 0.31f;
    private const float PressureTolerance = 0.05f;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly MeasurementStats acceleration = new MeasurementStats("Акселерометр");
    private readonly MeasurementStats gyro = new MeasurementStats("Гироскоп");
    private readonly MeasurementStats magnetometer = new MeasurementStats("Магнитометр");
    private readonly MeasurementStats barometer = new MeasurementStats("Барометр");
    private readonly MeasurementStats range = new MeasurementStats("Дальномер");
    private readonly MeasurementStats gpsPosition = new MeasurementStats("GPS position NED");
    private readonly MeasurementStats gpsVelocity = new MeasurementStats("GPS velocity NED");
    private readonly MeasurementStats gpsCourse = new MeasurementStats("GPS speed and course");

    private Rigidbody body;
    private string imuTopic;
    private string gpsTopic;
    private Vector3 gpsOriginUnity;
    private long lastImuTimestamp;
    private long lastGpsTimestamp;
    private long lastRangeTimestamp;
    private Vector3 previousVelocity;
    private bool hasPreviousVelocity;
    private int unmatchedImuSteps;
    private int unmatchedGpsSteps;
    private int gpsMetadataFailures;
    private int unsaturatedRangeSamples;
    private double nextReport;
    private bool running;
    private bool rangeConfigured;
    private StreamWriter csv;

    private void OnEnable()
    {
        if (drone == null || !drone.TryGetComponent(out body))
        {
            Debug.LogError("[VirtualSensorTest] Назначьте PX4Quadrocopter с Rigidbody в поле Drone.", this);
            enabled = false;
            return;
        }

        imuTopic = $"HILSensor_{drone.droneId}";
        gpsTopic = $"HILGPS_{drone.droneId}";
        gpsOriginUnity = drone.transform.position;
        lastImuTimestamp = DataBroker.GetState<HILSensor>(imuTopic).timestamp;
        lastGpsTimestamp = DataBroker.GetState<HILGPS>(gpsTopic).timestamp;
        hasPreviousVelocity = false;
        rangeConfigured = rangeFinder != null && !string.IsNullOrWhiteSpace(rangeFinder.topicName)
            && IsFinite(rangeFinder.minRange) && IsFinite(rangeFinder.maxRange)
            && rangeFinder.minRange >= 0f && rangeFinder.maxRange > rangeFinder.minRange;

        if (rangeConfigured)
            lastRangeTimestamp = DataBroker.GetState<Float32>(rangeFinder.topicName).timestamp;
        else if (rangeFinder != null)
            Debug.LogError("[VirtualSensorTest] Проверьте имя топика и пределы дальномера в Inspector.", this);

        ResetSummary();
        nextReport = Time.realtimeSinceStartupAsDouble + reportIntervalSeconds;
        running = true;
        RunAxisChecks();
        if (drone.publishHIL_GPS)
            Debug.Log($"[VirtualSensorTest] Проверка включена: {gpsTopic}.", this);
        else
            Debug.LogWarning("[VirtualSensorTest] GPS-тест не запущен: включите Publish HIL GPS на объекте дрона.", this);
        OpenCsv();
    }

    private void Start()
    {
        if (cameraPublisher != null)
            ReportCamera();
    }

    private void FixedUpdate()
    {
        if (!running || body == null)
            return;

        Vector3 velocity = body.linearVelocity;
        Vector3 worldAcceleration = (velocity - previousVelocity) / Time.fixedDeltaTime;
        previousVelocity = velocity;
        bool canCheckAcceleration = hasPreviousVelocity;
        hasPreviousVelocity = true;

        // PX4Quadrocopter stamps HILSensor with Time.timeAsDouble in FixedUpdate.
        long physicsTimestamp = (long)(Time.timeAsDouble * 1_000_000.0);
        HILSensor sample = DataBroker.GetState<HILSensor>(imuTopic);
        if (sample.timestamp != physicsTimestamp || sample.timestamp == lastImuTimestamp)
        {
            unmatchedImuSteps++;
            return;
        }
        lastImuTimestamp = sample.timestamp;

        Transform frame = drone.transform;
        if (canCheckAcceleration)
        {
            Vector3 specificForce = worldAcceleration - Physics.gravity;
            Record(acceleration, "accel_m_s2", sample.timestamp,
                new Vector3(sample.accel_x, sample.accel_y, sample.accel_z), ProjectToFrd(frame, specificForce),
                Vector3.one * (Mathf.Abs(drone.accelNoiseFactor) + AccelerationMargin));
        }

        // Angular velocity is an axial vector. Unity -> FRD reverses handedness.
        Record(gyro, "gyro_rad_s", sample.timestamp,
            new Vector3(sample.gyro_x, sample.gyro_y, sample.gyro_z), -ProjectToFrd(frame, body.angularVelocity),
            Vector3.one * (Mathf.Abs(drone.gyroNoiseFactor) + GyroMargin));

        Vector3 fieldNed = drone.magWorldNED;
        Vector3 fieldUnity = new Vector3(fieldNed.y, -fieldNed.z, fieldNed.x);
        Record(magnetometer, "mag_gauss", sample.timestamp,
            new Vector3(sample.mag_x, sample.mag_y, sample.mag_z), ProjectToFrd(frame, fieldUnity),
            Vector3.one * (Mathf.Abs(drone.magNoiseFactor) + MagneticMargin));

        float expectedAltitude = drone.homeAltitudeMSL + frame.position.y;
        // Pressure is derived from the same noisy altitude by the virtual barometer.
        float expectedPressure = 1013.25f * Mathf.Pow(1f - 0.0000225577f * sample.pressure_alt, 5.25588f);
        Record(barometer, "baro_alt_m_pressure_hpa_temperature_c", sample.timestamp,
            new Vector3(sample.pressure_alt, sample.abs_pressure, sample.temperature),
            new Vector3(expectedAltitude, expectedPressure, 20f),
            new Vector3(BarometerAltitudeTolerance, PressureTolerance, 0.01f));

        if (drone.publishHIL_GPS)
            CheckGps(sample.timestamp, frame);
    }

    private void CheckGps(long imuTimestamp, Transform frame)
    {
        HILGPS sample = DataBroker.GetState<HILGPS>(gpsTopic);
        if (sample.timestamp != imuTimestamp || sample.timestamp == lastGpsTimestamp)
        {
            unmatchedGpsSteps++;
            return;
        }
        lastGpsTimestamp = sample.timestamp;

        Vector3 unityOffset = frame.position - gpsOriginUnity;
        Vector3 expectedPositionNed = UnityToNEDConverter.PositionToNED(unityOffset);
        float latitudeScale = 111320f;
        float longitudeScale = latitudeScale * Mathf.Cos((float)drone.homeLatitude * Mathf.Deg2Rad);
        var (homeLatitude, homeLongitude) = UnityToNEDConverter.UnityPosToLatLon(
            Vector3.zero, drone.homeLatitude, drone.homeLongitude);
        Vector3 measuredPositionNed = new Vector3(
            (float)(sample.lat * 1e-7 - homeLatitude) * latitudeScale,
            (float)(sample.lon * 1e-7 - homeLongitude) * longitudeScale,
            (drone.homeAltitudeMSL - sample.alt * 0.001f));

        Record(gpsPosition, "gps_position_ned_m", sample.timestamp, measuredPositionNed, expectedPositionNed,
            new Vector3(Mathf.Max(0.03f, latitudeScale * 2e-7f),
                Mathf.Max(0.03f, Mathf.Abs(longitudeScale) * 2e-7f), 0.02f));
        Record(gpsVelocity, "gps_velocity_ned_m_s", sample.timestamp,
            new Vector3(sample.vn, sample.ve, sample.vd) * 0.01f,
            UnityToNEDConverter.VelocityToNED(body.linearVelocity), Vector3.one * 0.011f);

        Vector3 velocityNed = UnityToNEDConverter.VelocityToNED(body.linearVelocity);
        float expectedSpeed = new Vector2(velocityNed.x, velocityNed.y).magnitude;
        float expectedCourse = Mathf.Repeat(Mathf.Atan2(velocityNed.y, velocityNed.x) * Mathf.Rad2Deg, 360f);
        float measuredCourseRad = sample.cog * 0.01f * Mathf.Deg2Rad;
        float expectedCourseRad = expectedCourse * Mathf.Deg2Rad;
        Record(gpsCourse, "gps_groundspeed_m_s_cog_deg", sample.timestamp,
            new Vector3(sample.vel * 0.01f,
                expectedSpeed >= MinimumCourseSpeedMps ? Mathf.Sin(measuredCourseRad) : 0f,
                expectedSpeed >= MinimumCourseSpeedMps ? Mathf.Cos(measuredCourseRad) : 0f),
            new Vector3(expectedSpeed,
                expectedSpeed >= MinimumCourseSpeedMps ? Mathf.Sin(expectedCourseRad) : 0f,
                expectedSpeed >= MinimumCourseSpeedMps ? Mathf.Cos(expectedCourseRad) : 0f),
            Vector3.one * 0.011f);

        if (sample.fix_type != 3 || sample.satellites_visible != 12 || sample.eph != 100 || sample.epv != 100)
            gpsMetadataFailures++;
    }

    private void LateUpdate()
    {
        if (!running)
            return;

        if (rangeConfigured && rangeFinder != null)
            CheckRange();

        double now = Time.realtimeSinceStartupAsDouble;
        if (now >= nextReport)
        {
            ReportSummary();
            ResetSummary();
            nextReport = now + reportIntervalSeconds;
        }
    }

    private void CheckRange()
    {
        Float32 sample = DataBroker.GetState<Float32>(rangeFinder.topicName);
        if (sample.timestamp == 0 || sample.timestamp == lastRangeTimestamp)
            return;
        lastRangeTimestamp = sample.timestamp;

        // RangeFinder publishes in Update and uses the same forward ray and default physics mask.
        Transform sensor = rangeFinder.transform;
        bool hit = Physics.Raycast(sensor.position, sensor.forward, out RaycastHit result, rangeFinder.maxRange);
        float expected = hit ? Mathf.Max(result.distance, rangeFinder.minRange) : rangeFinder.maxRange;
        float noise = Mathf.Abs(rangeFinder.noiseFactor);
        bool unsaturated = hit && result.distance >= rangeFinder.minRange
            && result.distance < rangeFinder.maxRange - noise;
        if (unsaturated)
            unsaturatedRangeSamples++;

        Record(range, unsaturated ? "range_m" : "range_limit_m", sample.timestamp,
            new Vector3(sample.value, 0f, 0f), new Vector3(expected, 0f, 0f),
            new Vector3(noise + RangeMargin, 0f, 0f));
    }

    [ContextMenu("Run axis checks")]
    private void RunAxisChecks()
    {
        Vector3[] unityAxes = { Vector3.right, Vector3.up, Vector3.forward };
        Vector3[] frdAxes = { Vector3.up, Vector3.back, Vector3.right };
        const string axisNames = "XYZ";
        bool passed = true;

        for (int i = 0; i < unityAxes.Length; i++)
        {
            Vector3 axis = unityAxes[i];
            char axisName = axisNames[i];
            passed &= CheckAxis($"PositionToNED +{axisName}", UnityToNEDConverter.PositionToNED(axis), frdAxes[i]);
            passed &= CheckAxis($"VelocityToNED +{axisName}", UnityToNEDConverter.VelocityToNED(axis), frdAxes[i]);
            passed &= CheckAxis($"AngularVelocityToNED +{axisName}",
                UnityToNEDConverter.AngularVelocityToNED(axis), -frdAxes[i]);
            passed &= CheckAxis($"BodyUnityToBodyFRD +{axisName}", UnityToNEDConverter.BodyUnityToBodyFRD(axis), frdAxes[i]);
            passed &= CheckAxis($"AngularVelocityToBodyFRD +{axisName}",
                UnityToNEDConverter.AngularVelocityToBodyFRD(axis), -frdAxes[i]);

            Quaternion unityRotation = Quaternion.AngleAxis(90f, axis);
            Quaternion nedRotation = UnityToNEDConverter.RotationToNED(unityRotation);
            for (int j = 0; j < unityAxes.Length; j++)
            {
                Vector3 rotated = unityRotation * unityAxes[j];
                Vector3 expected = new Vector3(rotated.z, rotated.x, -rotated.y);
                passed &= CheckAxis($"RotationToNED +90° {axisName}, вектор +{axisNames[j]}",
                    nedRotation * frdAxes[j], expected);
            }
        }

        Quaternion currentRotation = Quaternion.AngleAxis(31f, new Vector3(1f, 2f, -3f).normalized)
            * drone.transform.rotation;
        Quaternion currentNedRotation = UnityToNEDConverter.RotationToNED(currentRotation);
        Vector3[] bodyUnityAxes = { Vector3.right, Vector3.up, Vector3.forward };
        for (int bodyAxis = 0; bodyAxis < bodyUnityAxes.Length; bodyAxis++)
        {
            Vector3 unityDirection = currentRotation * bodyUnityAxes[bodyAxis];
            Vector3 expected = new Vector3(unityDirection.z, unityDirection.x, -unityDirection.y);
            passed &= CheckAxis($"RotationToNED текущая поза body-{axisNames[bodyAxis]}",
                currentNedRotation * frdAxes[bodyAxis], expected);
        }

        if (passed)
            Debug.Log("[VirtualSensorTest] PASS преобразования осей Unity → NED/FRD.", this);
    }

    private bool CheckAxis(string name, Vector3 actual, Vector3 expected)
    {
        if ((actual - expected).sqrMagnitude <= AxisTolerance * AxisTolerance)
            return true;

        Debug.LogError($"[VirtualSensorTest] FAIL {name}. Получено {actual}, ожидалось {expected}.", this);
        return false;
    }

    private void ReportCamera()
    {
        Camera camera = cameraPublisher.sourceCamera;
        RenderTexture target = camera != null ? camera.targetTexture : null;
        if (target == null || target.width <= 0 || target.height <= 0)
        {
            Debug.LogWarning("[VirtualSensorTest] Камера не назначена или RenderTexture не создана.", this);
            return;
        }

        Matrix4x4 projection = camera.projectionMatrix;
        bool compatible = target.width == 680 && target.height == 420 && !camera.orthographic;
        string message = $"[VirtualSensorTest] Camera_{cameraPublisher.cameraID}: {target.width}×{target.height}, "
            + $"perspective={!camera.orthographic}, target FPS={cameraPublisher.targetFPS:F1}. "
            + $"Наклон от нижней оси корпуса {Vector3.Angle(camera.transform.forward, -drone.transform.up):F1}°.";

        if (!camera.orthographic)
        {
            // Intrinsics in the top-left image coordinates used after the Python vertical flip.
            float fx = Mathf.Abs(projection.m00) * target.width / 2f;
            float fy = Mathf.Abs(projection.m11) * target.height / 2f;
            float cx = (1f - projection.m02) * target.width / 2f;
            float cy = (1f + projection.m12) * target.height / 2f;
            message += $" fx={fx:F2}, fy={fy:F2}, cx={cx:F2}, cy={cy:F2} px.";
        }

        if (compatible)
            Debug.Log(message, this);
        else
            Debug.LogWarning(message + " Bridge_OF ожидает перспективный кадр 680×420.", this);

        if (cameraPublisher.cameraID != drone.droneId)
            Debug.LogWarning($"[VirtualSensorTest] Bridge_OF для дрона {drone.droneId} читает Camera_{drone.droneId}.", this);
    }

    private static Vector3 ProjectToFrd(Transform frame, Vector3 vector)
    {
        return new Vector3(Vector3.Dot(vector, frame.forward), Vector3.Dot(vector, frame.right),
            -Vector3.Dot(vector, frame.up));
    }

    private void Record(MeasurementStats stats, string channel, long timestamp, Vector3 actual,
        Vector3 expected, Vector3 tolerance)
    {
        bool passed = stats.Add(actual, expected, tolerance);
        if (csv == null)
            return;

        try
        {
            csv.WriteLine(FormattableString.Invariant(
                $"{channel},{Time.timeAsDouble:R},{timestamp},{actual.x:R},{actual.y:R},{actual.z:R},{expected.x:R},{expected.y:R},{expected.z:R},{(passed ? 1 : 0)}"));
        }
        catch (IOException error)
        {
            Debug.LogWarning($"[VirtualSensorTest] Запись CSV прервана. {error.Message}", this);
            CloseCsv();
        }
    }

    private void ReportSummary()
    {
        var report = new StringBuilder($"[VirtualSensorTest] {imuTopic}. Шагов без нового IMU того же времени: {unmatchedImuSteps}.");
        acceleration.AppendTo(report);
        gyro.AppendTo(report);
        magnetometer.AppendTo(report);
        barometer.AppendTo(report);
        if (drone.publishHIL_GPS)
        {
            gpsPosition.AppendTo(report);
            gpsVelocity.AppendTo(report);
            gpsCourse.AppendTo(report);
            report.Append($" GPS без совпавшей метки={unmatchedGpsSteps}, ошибок метаданных={gpsMetadataFailures}.");
        }
        if (rangeConfigured)
        {
            range.AppendTo(report);
            report.Append($" Отсчётов внутри диапазона: {unsaturatedRangeSamples}/{range.Count}.");
        }

        bool warning = unmatchedImuSteps > 0 || acceleration.Count == 0 || acceleration.Failures > 0
            || gyro.Failures > 0 || magnetometer.Failures > 0 || barometer.Failures > 0
            || (drone.publishHIL_GPS && (unmatchedGpsSteps > 0 || gpsMetadataFailures > 0
                || gpsPosition.Failures > 0 || gpsVelocity.Failures > 0 || gpsCourse.Failures > 0))
            || (rangeConfigured && (unsaturatedRangeSamples == 0 || range.Failures > 0));
        if (warning)
            Debug.LogWarning(report.ToString(), this);
        else
            Debug.Log(report.ToString(), this);
    }

    private void ResetSummary()
    {
        unmatchedImuSteps = 0;
        unmatchedGpsSteps = 0;
        gpsMetadataFailures = 0;
        unsaturatedRangeSamples = 0;
        acceleration.Reset();
        gyro.Reset();
        magnetometer.Reset();
        barometer.Reset();
        range.Reset();
        gpsPosition.Reset();
        gpsVelocity.Reset();
        gpsCourse.Reset();
    }

    private void OpenCsv()
    {
        if (!writeCsv)
            return;

        string name = $"virtual-sensors-{drone.droneId}-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", Invariant)}.csv";
        string path = Path.Combine(Application.persistentDataPath, name);
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            csv = new StreamWriter(path, false, new UTF8Encoding(false), 8192);
            // IMU timestamps are simulation microseconds. RangeFinder timestamps are Unix milliseconds.
            csv.WriteLine("channel,sim_time_s,source_timestamp,measured_x,measured_y,measured_z,expected_x,expected_y,expected_z,pass");
            Debug.Log($"[VirtualSensorTest] CSV: {path}", this);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            Debug.LogWarning($"[VirtualSensorTest] Не удалось открыть CSV. {error.Message}", this);
            CloseCsv();
        }
    }

    private void CloseCsv()
    {
        try
        {
            csv?.Dispose();
        }
        catch (IOException error)
        {
            Debug.LogWarning($"[VirtualSensorTest] Ошибка закрытия CSV. {error.Message}", this);
        }
        finally
        {
            csv = null;
        }
    }

    private void OnDisable()
    {
        if (!running)
            return;

        running = false;
        if (gyro.Count + range.Count + unmatchedImuSteps > 0)
            ReportSummary();
        CloseCsv();
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private sealed class MeasurementStats
    {
        private readonly string name;
        private Vector3 maxError;
        private string firstFailure;

        public int Count { get; private set; }
        public int Failures { get; private set; }

        public MeasurementStats(string name)
        {
            this.name = name;
        }

        public bool Add(Vector3 actual, Vector3 expected, Vector3 tolerance)
        {
            Count++;
            Vector3 error = actual - expected;
            error = new Vector3(Mathf.Abs(error.x), Mathf.Abs(error.y), Mathf.Abs(error.z));
            bool finite = IsFinite(error.x) && IsFinite(error.y) && IsFinite(error.z);
            if (finite)
                maxError = Vector3.Max(maxError, error);

            if (finite && error.x <= tolerance.x && error.y <= tolerance.y && error.z <= tolerance.z)
                return true;

            Failures++;
            if (firstFailure == null)
                firstFailure = $"получено {actual.ToString("F4")}, ожидалось {expected.ToString("F4")}";
            return false;
        }

        public void AppendTo(StringBuilder report)
        {
            report.AppendLine();
            if (Count == 0)
            {
                report.Append($"{name}: нет отсчётов.");
                return;
            }

            report.Append($"{name}: ошибок {Failures}/{Count}, max |Δ| {maxError.ToString("F4")}.");
            if (firstFailure != null)
                report.Append($" Первый сбой: {firstFailure}.");
        }

        public void Reset()
        {
            Count = 0;
            Failures = 0;
            maxError = Vector3.zero;
            firstFailure = null;
        }
    }
}