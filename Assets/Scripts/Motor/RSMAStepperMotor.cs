using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Implements properties and functionality of stepper motor
/// </summary>
public class RSMAStepperMotor : RSMADataTransferSlave
{
    //����� ������
    public StepperMotorType StepperType;

    //����� ��������������
    public int StepCount { get; set; }


    void Start()
    {
        
    }

    void FixedUpdate()
    {
        
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

        rsma_stepper_motor.StepperType = (StepperMotorType)EditorGUILayout.EnumPopup("��� �������� ���������: ", rsma_stepper_motor.StepperType);
        EditorGUILayout.Space(10);

        if (rsma_stepper_motor.StepperType == StepperMotorType.VariableMagnets) VariableMagnets(rsma_stepper_motor);
        if (rsma_stepper_motor.StepperType == StepperMotorType.PermanentMagnents) PermanentMagnents(rsma_stepper_motor);
        if (rsma_stepper_motor.StepperType == StepperMotorType.Hybrid) Hybrid(rsma_stepper_motor);
    }

    private void VariableMagnets(RSMAStepperMotor rsm)
    {
        string info_box = "";

        EditorGUILayout.LabelField("������� ��������� � ���������� ��������� ��������������");

        rsm.StepCount = EditorGUILayout.IntSlider("���������� �����: ", rsm.StepCount, 12, 24);
        EditorGUILayout.Space(10);

        info_box += "��������������:\n";
        info_box += $"���� ����: {rsm.GetStepAngle()}�\n";

        EditorGUILayout.HelpBox(info_box, MessageType.None);
        //EditorGUILayout.LabelField($"���� ����: {rsm.GetStepAngle()}�");

        EditorGUILayout.HelpBox("������� ��������� � ���������� ��������� �������������� ����� ��������� ������� �� ������� � ����� �������� ����� �� �������������� ���������. ��������������� ������ �����������. ��� �������� �� ������� ����� ����� 4 �����, � ������ ����� 6 �������. ��������� ����� 3 ����������� �������, ������ �� ������� �������� �� ���� ��������������� ������� �������.", MessageType.Info);
    }

    private void PermanentMagnents(RSMAStepperMotor rsm)
    {
        //
        EditorGUILayout.LabelField("������� ��������� � ����������� ���������");

        rsm.StepCount = EditorGUILayout.IntSlider("���������� �����: ", rsm.StepCount, 24, 48);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox("������� ��������� � ����������� ��������� ������� �� �������, ������� ����� �������, � ������, ����������� ���������� �������. ������������ ������ ������ ����� ������������� ����� � ����������� ����������� ��� ���������. ��������� ��������������� ������ � ����� ���������� �������������� ������� ��������� ����� �, ��� ���������, ������� ������, ��� � ���������� � ���������� ��������� ��������������.", MessageType.Info);
    }

    private void Hybrid(RSMAStepperMotor rsm)
    {
        EditorGUILayout.LabelField("��������� ������� ���������");

        rsm.StepCount = EditorGUILayout.IntSlider("���������� �����: ", rsm.StepCount, 100, 400);
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