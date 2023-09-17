using UnityEditor;
using UnityEngine;

public class RSMAEncoder : RSMADataTransferSlave
{
    //Общие данные
    public EncoderType EncoderTypeObj { get; set; }
    public EncoderRangeType EncoderRangeType { get; set; }
    public AxisEnum AxisDirection { get; set; } //С какой стороны будет находится энкодер относительно мотора
    public EncoderOutput OutPut { get; set; } = EncoderOutput.Output3;
    public bool AutoAxis { get; set; } //Автоматическое определение рабочей стороны


    //Данные инкрементального энкодера
    public float Distance = 2;
    public float EncoderResolution = 1;

    public GameObject Shaft;
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
    /// Получить скорость вращения вала в градусах/секунду
    /// </summary>
    public float GetVelocity()
    {
        return Motor.velocity;
    }

    /// <summary>
    /// Получить направление вала
    /// </summary>
    public int GetSide()
    {
        if (Motor.velocity == 0) return 0;
        if (Motor.motor.targetVelocity < 0) return 1;
        if (Motor.motor.targetVelocity > 0) return 2;
        return 0;
    }

    public string GetHZ()
    {
        if (Motor == null || !Motor.useMotor) return "Motor is NULL";
        return $"{(int)(Motor.velocity * EncoderResolution)} Гц";
    }

    public string GetMeasureAngle()
    {
        if (EncoderResolution == 0) return "NaN";
        return $"{360 / EncoderResolution}°";
    }

    /// <summary>
    /// Получить угол вращения
    /// </summary>
    /// <returns></returns>
    public float GetAngle()
    {
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

/// <summary>
/// 1-фазный энкодер - определяет чистоту вращения двигателя
/// 2-фазный энкодер - определяет 1фазу и направление вращения двигателя
/// 3-фазный энкодер - определяет 1фазу и 2фазу и угол поворота
/// </summary>
public enum EncoderOutput
{
    Output1 = 0,
    Output2 = 1,
    Output3 = 2
}

public enum EncoderType
{
    Incremental = 0,
    Absolute = 1
}

public enum EncoderRangeType
{
    Limit = 0,
    Unlimit = 1
}