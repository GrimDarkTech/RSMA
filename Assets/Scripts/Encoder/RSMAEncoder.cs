using UnityEditor;
using UnityEngine;

public class RSMAEncoder : RSMADataTransferSlave
{
    /// <summary>
    /// Defines the type of encoder used in the simulation
    /// </summary>
    public EncoderType EncoderTypeObj { get; set; }
    /// <summary>
    /// Allows you to set up a limit on the rotation of the shaft
    /// </summary>
    public EncoderRangeType EncoderRangeType { get; set; }
    /// <summary>
    /// Determines which side the encoder will be located relative to the motor
    /// </summary>
    public AxisEnum AxisDirection { get; set; }
    /// <summary>
    /// Determines how much data the encoder transmits over the data transmitter
    /// </summary>
    public EncoderOutput OutPut { get; set; } = EncoderOutput.Output3;
    /// <summary>
    /// Automatic determination of the axis from which the encoder will be connected 
    /// to the motor
    /// </summary>
    public bool AutoAxis { get; set; } //Автоматическое определение рабочей стороны

    /// <summary>
    /// At what distance from the object the encoder will be located
    /// the distance can be negative and positive
    /// </summary>
    public float Distance = 2;
    /// <summary>
    /// Encoder resolution is the number of pulses per revolution (PPR) or bits 
    /// output by the encoder during one 360 degree revolution of the encoder 
    /// shaft or bore. 
    /// </summary>
    public float EncoderResolution = 1;

    /// <summary>
    /// the object of the shaft that will spin together with the motor
    /// </summary>
    public GameObject Shaft;
    /// <summary>
    /// Motor of the object under study
    /// </summary>
    public HingeJoint Motor;

    void FixedUpdate()
    {
        if (Motor == null) return;

        data = "";
        if ((int)OutPut >= 0) data += $"{GetVelocity()};";
        if ((int)OutPut >= 1) data += $"{GetSide()};";
        if ((int)OutPut >= 2) data += $"{GetAngle()};";

        float steps = 1 / Time.fixedDeltaTime;
        float speed = Motor.motor.targetVelocity;
        float side = Distance < 0 ? 1 : -1;


        Shaft.transform.Rotate(0, 0, side * (speed / steps), Space.Self);
    }

    /// <summary>
    /// Get the speed of rotation of the shaft in degrees per second
    /// </summary>
    public float GetVelocity()
    {
        if (Motor == null) return 0;
        return Motor.velocity;
    }

    /// <summary>
    /// Get the direction of the shaft
    /// 0 - no side
    /// 1 - right side
    /// 2 - left side
    /// </summary>
    public int GetSide()
    {
        if (Motor == null) return 0;
        if (Motor.velocity == 0) return 0;
        if (Motor.motor.targetVelocity < 0) return 1;
        if (Motor.motor.targetVelocity > 0) return 2;
        return 0;
    }

    /// <summary>
    /// Get the rotation frequency of the encoder shaft
    /// </summary>
    /// <returns></returns>
    public string GetHZ()
    {
        if (Motor == null || !Motor.useMotor) return "Motor is NULL";
        return $"{(int)(Motor.velocity * EncoderResolution)} Гц";
    }

    /// <summary>
    /// Get the calculated step angle with which the encoder 
    /// updates the data
    /// </summary>
    public string GetMeasureAngle()
    {
        if (EncoderResolution == 0) return "NaN";
        return $"{360 / EncoderResolution}°";
    }

    /// <summary>
    /// Get the angle of rotation of the encoder shaft, which 
    /// depends on the type of encoder
    /// </summary>
    /// <returns></returns>
    public float GetAngle()
    {
        if (Motor == null) return 0;
        float current_angle = Motor.angle;
        float measure_angle = 360 / EncoderResolution;
        if (Motor.angle < 0)
        {
            current_angle = 360 + Motor.angle;
        }
        if (EncoderTypeObj == EncoderType.Absolute) return current_angle;
        if (EncoderTypeObj == EncoderType.Incremental) return ((int)(current_angle / measure_angle)) * measure_angle;
        return 0;
    }

    public override string SendData()
    {
        return (data);
    }
}

/// <summary>
/// Settings for the unique display of encoder data. 
/// Deleted in the final build
/// </summary>
#if UNITY_EDITOR
[CustomEditor(typeof(RSMAEncoder))]
public class RSMAEncoderEditor : Editor
{

    public override void OnInspectorGUI()
    {
        RSMAEncoder rsma_encoder = (RSMAEncoder)target;
        rsma_encoder.EncoderTypeObj = (EncoderType)EditorGUILayout.EnumPopup("Тип энкодера: ", rsma_encoder.EncoderTypeObj);

        SharedFunctions(rsma_encoder);

        if (rsma_encoder.EncoderTypeObj == EncoderType.Incremental) IncrementalEncoder(rsma_encoder);
        if (rsma_encoder.EncoderTypeObj == EncoderType.Absolute) AbsouluteEncoder(rsma_encoder);

    }

    private void IncrementalEncoder(RSMAEncoder rsma_encoder)
    {
        EditorGUILayout.LabelField("Инкрементальный энкодер");

        float Local_EncoderRes = EditorGUILayout.FloatField("Разрешение энкодера имп/об: ", rsma_encoder.EncoderResolution); //Кол-во импульсов на оборот
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Частота выходных импульсов: {rsma_encoder.GetHZ()}"); //Данные о частоте вращения
        EditorGUILayout.LabelField($"Угол за импульс: {rsma_encoder.GetMeasureAngle()}"); //Данные об угле за импульс
        EditorGUILayout.Space(5);
        if ((int)rsma_encoder.OutPut >= 0) EditorGUILayout.LabelField($"Выход 1. Скорость вращения вала: {rsma_encoder.GetVelocity()} град/сек");
        if ((int)rsma_encoder.OutPut >= 1) EditorGUILayout.LabelField($"Выход 2. Направление вращения: {(rsma_encoder.GetSide() == 1 ? "Право" : (rsma_encoder.GetSide() == 2 ? "Лево" : "Не вращается"))}");
        if ((int)rsma_encoder.OutPut >= 2) EditorGUILayout.LabelField($"Выход 3. Угол поворота: {rsma_encoder.GetAngle()}°");

        EditorGUILayout.HelpBox("Инкрементный энкодер - формирует импульсы, количество \nкоторых соответствует повороту вала на определенный угол.", MessageType.Info);

        if (Local_EncoderRes < 0) Local_EncoderRes = 0;
        rsma_encoder.EncoderResolution = Local_EncoderRes;
    }

    private void AbsouluteEncoder(RSMAEncoder rsma_encoder)
    {
        EditorGUILayout.LabelField("Абсолютный энкодер энкодер");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Частота выходных импульсов: {rsma_encoder.GetHZ()}"); //Данные о частоте вращения
        EditorGUILayout.Space(5);
        if ((int)rsma_encoder.OutPut >= 0) EditorGUILayout.LabelField($"Выход 1. Скорость вращения вала: {rsma_encoder.GetVelocity()} град/сек");
        if ((int)rsma_encoder.OutPut >= 1) EditorGUILayout.LabelField($"Выход 2. Направление вращения: {(rsma_encoder.GetSide() == 1 ? "Право" : (rsma_encoder.GetSide() == 2 ? "Лево" : "Не вращается"))}");
        if ((int)rsma_encoder.OutPut >= 2) EditorGUILayout.LabelField($"Выход 3. Угол поворота: {rsma_encoder.GetAngle()}°");

        EditorGUILayout.HelpBox("Абсолютный энкодер – это датчик углового положения, \nкоторый выдаёт информацию о положении в виде многоразрядного цифрового кода. \nКаждый код является уникальным в пределах диапазона измеряемых угловых положений.", MessageType.Info);
    }

    private void SharedFunctions(RSMAEncoder rsma_encoder)
    {
        rsma_encoder.Motor = (HingeJoint)EditorGUILayout.ObjectField("Мотор:", rsma_encoder.Motor, typeof(HingeJoint));
        rsma_encoder.Shaft = (GameObject)EditorGUILayout.ObjectField("Вал:", rsma_encoder.Shaft, typeof(GameObject));
        EditorGUILayout.Space(10);
        rsma_encoder.AutoAxis = EditorGUILayout.Toggle("Авто определение направляющей оси: ", rsma_encoder.AutoAxis);
        if (!rsma_encoder.AutoAxis)
            rsma_encoder.AxisDirection = (AxisEnum)EditorGUILayout.EnumPopup("Направляющая ось: ", rsma_encoder.AxisDirection);
        rsma_encoder.Distance = EditorGUILayout.FloatField("Расстояние до мотора: ", rsma_encoder.Distance);
        rsma_encoder.OutPut = (EncoderOutput)EditorGUILayout.EnumPopup("Количество выходов: ", rsma_encoder.OutPut);

        SetAxisPos(rsma_encoder);

        EditorGUILayout.Space(20);
    }

    private void SetAxisPos(RSMAEncoder rsma_encoder)
    {
        if (rsma_encoder.Motor == null) return;
        if (Application.isPlaying) return;

        rsma_encoder.transform.position = rsma_encoder.Motor.transform.position;

        if (rsma_encoder.AutoAxis)
        {
            var local_axis =
                rsma_encoder.Motor.axis.normalized.x * rsma_encoder.Motor.transform.right +
                rsma_encoder.Motor.axis.normalized.y * rsma_encoder.Motor.transform.up +
                rsma_encoder.Motor.axis.normalized.z * rsma_encoder.Motor.transform.forward
                ;

            rsma_encoder.transform.position += local_axis * rsma_encoder.Distance;
            rsma_encoder.transform.LookAt(rsma_encoder.Motor.transform.position);
            return;
        }

        switch (rsma_encoder.AxisDirection)
        {
            case AxisEnum.X:
                rsma_encoder.transform.position += rsma_encoder.Motor.transform.right * rsma_encoder.Distance;
                break;
            case AxisEnum.Y:
                rsma_encoder.transform.position += rsma_encoder.Motor.transform.up * rsma_encoder.Distance;
                break;
            case AxisEnum.Z:
                rsma_encoder.transform.position += rsma_encoder.Motor.transform.forward * rsma_encoder.Distance;
                break;
        }
        rsma_encoder.transform.LookAt(rsma_encoder.Motor.transform.position);
    }

    //private void SetAxis
}
#endif

/// <summary>
/// 1-phase encoder - determines the engine speed
/// 2-phase encoder - determines 1 phase and the direction of rotation of the motor
/// 3-phase encoder - determines 1 phase and 2 phase and rotation angle
/// </summary>
public enum EncoderOutput
{
    Output1 = 0,
    Output2 = 1,
    Output3 = 2
}

/// <summary>
/// Type of working encoder
/// </summary>
public enum EncoderType
{
    Incremental = 0,
    Absolute = 1
}

/// <summary>
/// limits of the rotation
/// </summary>
public enum EncoderRangeType
{
    Limit = 0,
    Unlimit = 1
}