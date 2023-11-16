using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Implements properties and functionality of stepper motor
/// </summary>
[RequireComponent(typeof(HingeJoint), typeof(Rigidbody))]
public class RSMAStepperMotor : RSMADataTransferSlave
{
    /// <summary>
    /// 
    /// </summary>
    public StepperMotorType StepperType;
    public GameObject RotObj;
    public HingeJoint Motor;
    public Rigidbody RB;

    public int load_iterations = 1;
    public int time_loading = 0;

    private float cur_angle = 0;
    private float last_angle = 0;

    //Shared features
    public int StepCount;

    private void Awake()
    {
        Motor = GetComponent<HingeJoint>();
        RB = GetComponent<Rigidbody>();

        Motor.useLimits = true;
    }

    private void FixedUpdate()
    {
        if (time_loading > 0) { time_loading -= 1; return; }


        var lim = Motor.limits;
        lim.min = cur_angle;
        lim.max = cur_angle + GetStepAngle();

        Motor.limits = lim;

        if (lim.max >= 177)
        {
            cur_angle = (cur_angle + GetStepAngle()) - 177;
            time_loading = load_iterations;
            var euler = transform.localRotation.eulerAngles;
            transform.localRotation = Quaternion.Euler(euler.x, euler.y, 0);
            return;
        }

        if (Math.Round(transform.rotation.eulerAngles.z, 2) >= Math.Round(lim.max, 2))
        {
            cur_angle += GetStepAngle();
            time_loading = load_iterations;
        }

        if (last_angle > transform.rotation.eulerAngles.z)
        {
            last_angle = transform.rotation.eulerAngles.z;
        }

        if (RotObj != null)
        {
            if (Mathf.Abs(transform.rotation.eulerAngles.z - last_angle) > 5)
            Debug.Log(transform.rotation.eulerAngles.z +":"+ last_angle);
            RotObj.transform.RotateAround(transform.position, transform.forward, transform.rotation.eulerAngles.z - last_angle);
        }
        last_angle = transform.rotation.eulerAngles.z;
    }

    public override string SendData()
    {
        return base.SendData();
    }
    public float GetStepAngle()
    {
        return (360f / StepCount);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RSMAStepperMotor))]
public class RSMAStepperMotorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RSMAStepperMotor rsma_stepper_motor = (RSMAStepperMotor)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("RotObj"), new GUIContent("��������� ������"));
        serializedObject.FindProperty("StepperType").enumValueIndex = (int)(StepperMotorType)EditorGUILayout.EnumPopup("��� �������� ���������: ", rsma_stepper_motor.StepperType);
        EditorGUILayout.Space(10);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("load_iterations"), new GUIContent("�������� ��������:"));



        if (rsma_stepper_motor.StepperType == StepperMotorType.VariableMagnets) VariableMagnets(rsma_stepper_motor);
        if (rsma_stepper_motor.StepperType == StepperMotorType.PermanentMagnents) PermanentMagnents(rsma_stepper_motor);
        if (rsma_stepper_motor.StepperType == StepperMotorType.Hybrid) Hybrid(rsma_stepper_motor);

        string info_box = "";
        info_box += "��������������:\n";
        info_box += $"���� ����: {((RSMAStepperMotor)target).GetStepAngle()}�\n";
        EditorGUILayout.HelpBox(info_box, MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    private void VariableMagnets(RSMAStepperMotor rsm)
    {
        EditorGUILayout.LabelField("������� ��������� � ���������� ��������� ��������������");

        serializedObject.FindProperty("StepCount").intValue = EditorGUILayout.IntSlider("���������� �����: ", rsm.StepCount, 12, 24);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox("������� ��������� � ���������� ��������� �������������� ����� ��������� ������� �� ������� � ����� �������� ����� �� �������������� ���������. ��������������� ������ �����������. ��� �������� �� ������� ����� ����� 4 �����, � ������ ����� 6 �������. ��������� ����� 3 ����������� �������, ������ �� ������� �������� �� ���� ��������������� ������� �������.", MessageType.Info);
    }

    private void PermanentMagnents(RSMAStepperMotor rsm)
    {
        //
        EditorGUILayout.LabelField("������� ��������� � ����������� ���������");

        serializedObject.FindProperty("StepCount").intValue = EditorGUILayout.IntSlider("���������� �����: ", rsm.StepCount, 24, 48);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox("������� ��������� � ����������� ��������� ������� �� �������, ������� ����� �������, � ������, ����������� ���������� �������. ������������ ������ ������ ����� ������������� ����� � ����������� ����������� ��� ���������. ��������� ��������������� ������ � ����� ���������� �������������� ������� ��������� ����� �, ��� ���������, ������� ������, ��� � ���������� � ���������� ��������� ��������������.", MessageType.Info);
    }

    private void Hybrid(RSMAStepperMotor rsm)
    {
        EditorGUILayout.LabelField("��������� ������� ���������");

        serializedObject.FindProperty("StepCount").intValue = EditorGUILayout.IntSlider("���������� �����: ", rsm.StepCount, 100, 400);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox("��������� ������� ��������� �������� ����� ��������, ��� ��������� � ����������� ���������, ���� ��� ������������ ������� �������� ����, ������� ������ � ������� ��������.", MessageType.Info);
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
    VariableMagnets = 0, //� ���������� ��������� ���������������
    /// <summary>
    /// 
    /// </summary>
    PermanentMagnents = 1, //� ����������� ���������
    /// <summary>
    /// 
    /// </summary>
    Hybrid = 2, //� ����������� ���������
}