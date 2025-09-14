using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Implements properties and functionality of stepper motor
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Motors/Setting_up_stepper_motors.md")]
public class RSMAStepperMotor : RSMADataTransferSlave
{
    /// <summary>
    /// The type of stepper motor that changes the operation of the script
    /// </summary>
    public StepperMotorType StepperType;
    public GameObject RotationObject;
    public HingeJoint Hinje;
    public int StepCount;
    public float Start_pos = 0;
    public float Add_value = 0.1f;
    public float Time_wait = 0.1f;
    public float Gizmo_distance = 1;
    public bool Auto_set_distance = false;
    public float Distance_rot_obj = 0;
    public float Distance_shaft = 0;
    public int Side;
    public int Delay = 1;

    public Vector3 gizmo_test;

    private float max_angle = 177;
    private float min_angle = -3;
    private float delay_lefttime = 0;
    private float last_angle;
    public float display_pos;

    private void Awake()
    {
        Hinje.useLimits = true;
    }

    private void FixedUpdate()
    {
        bool val = default;
        Add_value = GetStepAngle();



        if (Side == 0) return;
        if (Side > 0)
        {
            val = Hinje.angle + 0.1f >= Hinje.limits.max;
        }
        else
        {
            val = Hinje.angle - 0.1f <= Hinje.limits.min;
        }

        var motor = Hinje.motor;
        motor.targetVelocity = Mathf.Abs(Hinje.motor.targetVelocity) * Side;
        Hinje.motor = motor;

        RotationObject.transform.RotateAround(transform.position, transform.forward, Hinje.transform.rotation.eulerAngles.z - last_angle);
        last_angle = Hinje.transform.rotation.eulerAngles.z;

        if (val)
        {

            if (delay_lefttime > 0)
            {
                delay_lefttime -= 1;
                return;
            }

            if (display_pos >= 360 || display_pos <= -360)
            {
                display_pos = 0;
            }

            if (Start_pos >= max_angle && Side > 0)
            {
                Hinje.useLimits = false;
                Hinje.transform.Rotate(0, 0, -(Hinje.angle + 3), Space.Self);
                last_angle = Hinje.transform.rotation.eulerAngles.z;
                Start_pos = min_angle;
            }
            if (Start_pos < min_angle && Side < 0)
            {
                Hinje.useLimits = false;
                Hinje.transform.Rotate(0, 0, 180, Space.Self);
                last_angle = Hinje.transform.rotation.eulerAngles.z;
                Start_pos = max_angle;
            }

            delay_lefttime = Delay;
        }
        else
        {
            return;
        }

        var limits = Hinje.limits;
        limits.min = Start_pos - Add_value;
        limits.max = Start_pos + Add_value;

        if (limits.min < min_angle) limits.min = min_angle;
        if (limits.min > max_angle) limits.min = max_angle;

        if (limits.max < min_angle) limits.max = min_angle;
        if (limits.max > max_angle) limits.max = max_angle;

        Hinje.limits = limits;

        Start_pos += Add_value * Side;
        display_pos += Add_value * Side;
        Hinje.useLimits = true;
    }

    public override string SendData()
    {
        return base.SendData();
    }
    public float GetStepAngle()
    {
        return (360f / StepCount);
    }

    public void SetUpHinjeJoint(bool recreate = false)
    {
        if (recreate)
        {
            if (Hinje != null)
            {
                DestroyImmediate(Hinje.gameObject);
            }
            var obj = Instantiate(new GameObject(), transform);
            obj.AddComponent<HingeJoint>();
        }

        if (Hinje == null) return;
        Hinje.connectedBody = gameObject.GetComponent<Rigidbody>();
        var mot = Hinje.motor;
        mot.force = 50;
        mot.targetVelocity = 50;
        Hinje.motor = mot;
        Hinje.useMotor = true;
        Hinje.useLimits = true;
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        var r = GetComponent<Renderer>();
        var col = Color.blue;
        col.a = 0.4f;
        float thickness = 1.5f;
        Vector3 start_point = Vector3.forward;
        Vector3 view_direction = Vector3.forward;

        view_direction = -transform.forward;
        start_point = transform.position + view_direction + view_direction * Gizmo_distance;

        Handles.color = col;
        Handles.DrawSolidArc(start_point, view_direction, transform.up, 360, 1);
        Handles.DrawWireArc(start_point, view_direction, transform.up, 360, 1);
        var d_current = Quaternion.AngleAxis(display_pos, transform.forward) * transform.up;
        var d_prev = Quaternion.AngleAxis(display_pos + Add_value, transform.forward) * transform.up;
        var d_next = Quaternion.AngleAxis(display_pos - Add_value, transform.forward) * transform.up;


        Handles.color = Color.white;
        int count = (int)(360 / Add_value);
        for (int i = 0; i < count; i++)
        {
            Handles.DrawLine(start_point, start_point + Quaternion.AngleAxis(Add_value * i, transform.forward) * transform.up, 1);
        }

        Handles.color = Color.red;
        Handles.DrawWireArc(start_point, view_direction, d_prev, Add_value * 2, 1, thickness);
        Handles.DrawLine(start_point, start_point + d_current, thickness);
        Handles.DrawLine(start_point, start_point + d_prev, thickness);
        Handles.DrawLine(start_point, start_point + d_next, thickness);

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        Handles.BeginGUI();
        Handles.Label(start_point + transform.right + transform.up, $"Угол шага: {GetStepAngle()}°\nТип шагового двигателя: Обычный°\nУгол поворота: {Mathf.Abs(display_pos)}", style);
    }

#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(RSMAStepperMotor))]
public class RSMAStepperMotorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RSMAStepperMotor rsma_stepper_motor = (RSMAStepperMotor)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("RotationObject"), new GUIContent("Вращаемый объект"));
        serializedObject.FindProperty("StepperType").enumValueIndex = (int)(StepperMotorType)EditorGUILayout.EnumPopup("Тип шагового двигателя: ", rsma_stepper_motor.StepperType);
        EditorGUILayout.Space(10);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Delay"), new GUIContent("Задержка шаговика:"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Side"), new GUIContent("Сторона вращения:"));
        EditorGUILayout.Space(10);
        serializedObject.FindProperty("Auto_set_distance").boolValue = GUILayout.Toggle(rsma_stepper_motor.Auto_set_distance, "Автоматическое выравнивание");
        if (serializedObject.FindProperty("Auto_set_distance").boolValue)
        {
            serializedObject.FindProperty("Distance_rot_obj").floatValue = EditorGUILayout.FloatField("Расстояние до вращаемого объекта: ", rsma_stepper_motor.Distance_rot_obj);
            serializedObject.FindProperty("Distance_shaft").floatValue = EditorGUILayout.FloatField("Расстояние до вала: ", rsma_stepper_motor.Distance_shaft);

            SetDistances(rsma_stepper_motor);
        }
        EditorGUILayout.Space(10);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Gizmo_distance"), new GUIContent("Дистанция гизмо:"));
        serializedObject.FindProperty("Add_value").floatValue = rsma_stepper_motor.GetStepAngle();

        if (rsma_stepper_motor.StepperType == StepperMotorType.VariableMagnets) VariableMagnets(rsma_stepper_motor);
        if (rsma_stepper_motor.StepperType == StepperMotorType.PermanentMagnents) PermanentMagnents(rsma_stepper_motor);
        if (rsma_stepper_motor.StepperType == StepperMotorType.Hybrid) Hybrid(rsma_stepper_motor);

        string info_box = "";
        info_box += "Характеристики:\n";
        info_box += $"Угол шага: {((RSMAStepperMotor)target).GetStepAngle()}°\n";

        if (GUILayout.Button("Сбросить настройки вала")) rsma_stepper_motor.SetUpHinjeJoint();

        if (rsma_stepper_motor.Hinje == null)
        {
            for (int i = 0; i < rsma_stepper_motor.transform.childCount; i++)
            {
                var obj = rsma_stepper_motor.transform.GetChild(i);
                if (obj.name.ToLower() == "shaft")
                {
                    if (obj.TryGetComponent<HingeJoint>(out var hinje))
                    {
                        rsma_stepper_motor.Hinje = hinje;
                        return;
                    }
                    else
                    {
                        rsma_stepper_motor.Hinje = obj.gameObject.AddComponent<HingeJoint>();
                        rsma_stepper_motor.SetUpHinjeJoint();
                    }
                }
            }

            string str_warning = "";
            str_warning += "Объект вала шагового двигателя не обнаружен. Добавьте готовый вал в объект срипта, либо сегенерируйте новый";

            EditorGUILayout.HelpBox(str_warning, MessageType.Warning);
        }

        EditorGUILayout.HelpBox(info_box, MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    private void VariableMagnets(RSMAStepperMotor rsm)
    {
        EditorGUILayout.LabelField("Шаговый двигатель с переменным магнитным сопротивлением");

        serializedObject.FindProperty("StepCount").intValue = EditorGUILayout.IntSlider("Количество шагов: ", rsm.StepCount, 12, 24);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox("Шаговые двигатели с переменным магнитным сопротивлением имеют несколько полюсов на статоре и ротор зубчатой формы из магнитомягкого материала. Намагниченность ротора отсутствует. Для простоты на рисунке ротор имеет 4 зубца, а статор имеет 6 полюсов. Двигатель имеет 3 независимые обмотки, каждая из которых намотана на двух противоположных полюсах статора.", MessageType.Info);
    }

    private void PermanentMagnents(RSMAStepperMotor rsm)
    {
        EditorGUILayout.LabelField("Шаговый двигатель с постоянными магнитами");

        serializedObject.FindProperty("StepCount").intValue = EditorGUILayout.IntSlider("Количество шагов: ", rsm.StepCount, 24, 48);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox("Шаговые двигатели с постоянными магнитами состоят из статора, который имеет обмотки, и ротора, содержащего постоянные магниты. Чередующиеся полюса ротора имеют прямолинейную форму и расположены параллельно оси двигателя. Благодаря намагниченности ротора в таких двигателях обеспечивается больший магнитный поток и, как следствие, больший момент, чем у двигателей с переменным магнитным сопротивлением.", MessageType.Info);
    }

    private void Hybrid(RSMAStepperMotor rsm)
    {
        EditorGUILayout.LabelField("Гибридный шаговый двигатель");

        serializedObject.FindProperty("StepCount").intValue = EditorGUILayout.IntSlider("Количество шагов: ", rsm.StepCount, 100, 400);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox("Гибридные шаговые двигатели являются более дорогими, чем двигатели с постоянными магнитами, зато они обеспечивают меньшую величину шага, больший момент и большую скорость.", MessageType.Info);
    }

    private void SetDistances(RSMAStepperMotor rsm)
    {
        var dist_rot = serializedObject.FindProperty("Distance_rot_obj").floatValue;
        var dist_shaft = serializedObject.FindProperty("Distance_shaft").floatValue;

        rsm.Hinje.transform.localPosition = -dist_shaft * rsm.transform.right;
        if (rsm.RotationObject != null)
        {
            rsm.transform.position = rsm.RotationObject.transform.position;
            rsm.transform.position = rsm.RotationObject.transform.position - rsm.transform.forward * dist_rot;
        }
    }
}
#endif
/// <summary>
/// 
/// </summary>
public enum StepperMotorType
{
    /// <summary>
    /// 
    /// </summary>
    VariableMagnets = 0, //С переменным магнитным сопростивлением
    /// <summary>
    /// 
    /// </summary>
    PermanentMagnents = 1, //С постоянными магнитами
    /// <summary>
    /// 
    /// </summary>
    Hybrid = 2, //С постоянными магнитами
}