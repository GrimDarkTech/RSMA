using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Implements properties and functionality of stepper motor
/// </summary>
public class RSMAStepperMotor : RSMADataTransferSlave
{
    //ќбщие данные
    public StepperMotorType StepperType;

    //ќбщие характеристики
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

        rsma_stepper_motor.StepperType = (StepperMotorType)EditorGUILayout.EnumPopup("“ип шагового двигател€: ", rsma_stepper_motor.StepperType);
        EditorGUILayout.Space(10);

        if (rsma_stepper_motor.StepperType == StepperMotorType.VariableMagnets) VariableMagnets(rsma_stepper_motor);
        if (rsma_stepper_motor.StepperType == StepperMotorType.PermanentMagnents) PermanentMagnents(rsma_stepper_motor);
        if (rsma_stepper_motor.StepperType == StepperMotorType.Hybrid) Hybrid(rsma_stepper_motor);
    }

    private void VariableMagnets(RSMAStepperMotor rsm)
    {
        string info_box = "";

        EditorGUILayout.LabelField("Ўаговый двигатель с переменным магнитным сопротивлением");

        rsm.StepCount = EditorGUILayout.IntSlider(" оличество шагов: ", rsm.StepCount, 12, 24);
        EditorGUILayout.Space(10);

        info_box += "’арактеристики:\n";
        info_box += $"”гол шага: {rsm.GetStepAngle()}∞\n";

        EditorGUILayout.HelpBox(info_box, MessageType.None);
        //EditorGUILayout.LabelField($"”гол шага: {rsm.GetStepAngle()}∞");

        EditorGUILayout.HelpBox("Ўаговые двигатели с переменным магнитным сопротивлением имеют несколько полюсов на статоре и ротор зубчатой формы из магнитом€гкого материала. Ќамагниченность ротора отсутствует. ƒл€ простоты на рисунке ротор имеет 4 зубца, а статор имеет 6 полюсов. ƒвигатель имеет 3 независимые обмотки, кажда€ из которых намотана на двух противоположных полюсах статора.", MessageType.Info);
    }

    private void PermanentMagnents(RSMAStepperMotor rsm)
    {
        //
        EditorGUILayout.LabelField("Ўаговый двигатель с посто€нными магнитами");

        rsm.StepCount = EditorGUILayout.IntSlider(" оличество шагов: ", rsm.StepCount, 24, 48);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox("Ўаговые двигатели с посто€нными магнитами состо€т из статора, который имеет обмотки, и ротора, содержащего посто€нные магниты. „ередующиес€ полюса ротора имеют пр€молинейную форму и расположены параллельно оси двигател€. Ѕлагодар€ намагниченности ротора в таких двигател€х обеспечиваетс€ больший магнитный поток и, как следствие, больший момент, чем у двигателей с переменным магнитным сопротивлением.", MessageType.Info);
    }

    private void Hybrid(RSMAStepperMotor rsm)
    {
        EditorGUILayout.LabelField("√ибридный шаговый двигатель");

        rsm.StepCount = EditorGUILayout.IntSlider(" оличество шагов: ", rsm.StepCount, 100, 400);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox("√ибридные шаговые двигатели €вл€ютс€ более дорогими, чем двигатели с посто€нными магнитами, зато они обеспечивают меньшую величину шага, больший момент и большую скорость.", MessageType.Info);
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
    VariableMagnets = 0, //— переменным магнитным сопростивлением
    /// <summary>
    /// 
    /// </summary>
    PermanentMagnents = 1, //— посто€нными магнитами
    /// <summary>
    /// 
    /// </summary>
    Hybrid = 2, //— посто€нными магнитами
}