using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public class Switch : MonoBehaviour
{
    public SwitchTypeEnum TypeEnum;

    /// <summary>
    /// The state of closure or opening of an electrical circuit.
    /// </summary>
    public bool MechState;

    /// <summary>
    /// Resistance to movement due to viscous friction.
    /// </summary>
    public float ButtonDamper = 10;
    /// <summary>
    /// The force returns to its previous shape after compression or stretching.
    /// </summary>
    public float ButtonSpring = 50;
    /// <summary>
    /// Maximum button pressing distance
    /// </summary>
    public float MaxLength = 0.015f;
    /// <summary>
    /// the activation point is within the initial position to the limit position 
    /// of the button, which is displayed as a percentage.
    /// </summary>
    public float HorButtonSlider = 100;

    //Роликовый рычаг
    /// <summary>
    /// Resistance to movement due to viscous friction for angle.
    /// </summary>
    public float AngularDamper = 10;
    /// <summary>
    /// The force returns to its previous shape after compression or stretching in 
    /// the angular view.
    /// </summary>
    public float AngularSpring = 50;
    /// <summary>
    /// The minimum angle at which the lever can turn.
    /// </summary>
    public float MinAngle = -10;
    /// <summary>
    /// The maximum angle at which the lever can turn
    /// </summary>
    public float MaxAngle = 50;
    /// <summary>
    /// the activation point is within the initial angle to the limit angle 
    /// of the lever, which is displayed as a percentage.
    /// </summary>
    public float HorLeverRollerSlider = 100;

    /// <summary>
    /// The joint is a lever responsible for simulating the behavior of the lever.
    /// </summary>
    public ConfigurableJoint Joint;
    /// <summary>
    /// Fulcrum of configurable joint.
    /// </summary>
    public Vector3 AnchorPos;
    /// <summary>
    /// Direction of the axis of rotation.
    /// </summary>
    public Vector3 Vector;

    /// <summary>
    /// The size of the gizmo that is displayed in the unity user interface.
    /// </summary>
    public float SizeGizmoSphere;

    /// <summary>
    /// An event that performs calculations depending on the type of limit switch.
    /// </summary>
    public Action DoCalculations;

    /// <summary>
    /// It is triggered when the limit switch switches to the on state
    /// </summary>
    public void SwitchOn()
    {
        MechState = true;
        Debug.Log("Состояние переключателя: Включено");
    }

    /// <summary>
    /// It is triggered when the limit switch switches to the off state
    /// </summary>
    public void SwitchOff()
    {
        MechState = false;
        Debug.Log("Состояние переключателя: Выключено");
    }

    /// <summary>
    /// Erases all settings of configurable joint of lever and sets new ones
    /// </summary>
    public void ResetJoint()
    {
        DestroyImmediate(gameObject.GetComponent<ConfigurableJoint>());
        Joint = gameObject.AddComponent<ConfigurableJoint>();
    }

    private void Start()
    {
        Vector = transform.localPosition;
    }

    private void Update()
    {
        if (DoCalculations != null) DoCalculations();
    }

    private void OnDrawGizmos()
    {
        switch (TypeEnum)
        {
            case SwitchTypeEnum.Plunger:
                GizmoPlunger();
                break;
            case SwitchTypeEnum.LeverRoller:
                GizmoLeverRoller();
                break;
            case SwitchTypeEnum.FlexibleRod:
                break;
        }
    }

    private void GizmoPlunger()
    {
        Vector3 anchor_pos = transform.position +
            new Vector3(
                AnchorPos.x * transform.localScale.x,
                AnchorPos.y * transform.localScale.y,
                AnchorPos.z * transform.localScale.z
                       );
        Vector3 max_length_pos = new Vector3
            (
                anchor_pos.x,
                anchor_pos.y - MaxLength,
                anchor_pos.z
            );
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(anchor_pos, SizeGizmoSphere);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(max_length_pos, SizeGizmoSphere);
    }

    private void GizmoLeverRoller()
    {
        Vector3 anchor_pos = transform.localPosition +
            new Vector3(
                AnchorPos.x * transform.localScale.x,
                AnchorPos.y * transform.localScale.y,
                AnchorPos.z * transform.localScale.z
                       );
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(anchor_pos, SizeGizmoSphere);
        Gizmos.DrawIcon(anchor_pos, "ServoRotate", false);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.color = new Color(0.8f, 0f, 0f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(Vector3.zero, transform.rotation, new Vector3(Joint.axis.x == 1 ? 0f : 1, Joint.axis.y == 1 ? 0f : 1, Joint.axis.z == 1 ? 0f : 1));
        Gizmos.DrawSphere(anchor_pos, SizeGizmoSphere * 5);
        Gizmos.matrix = oldMatrix;
    }
}


/// <summary>
/// Settings for the unique display of limit switch data. 
/// Deleted in the final build.
/// </summary>
[CustomEditor(typeof(Switch))]
public class SwitchEditor : Editor
{
    public override void OnInspectorGUI()
    {

        Switch switch_lever = (Switch)target;

        if (switch_lever.Joint == null)
        {
            switch_lever.Joint = switch_lever.GetComponent<ConfigurableJoint>();
        }

        ConfigurableJoint joint = switch_lever.GetComponent<ConfigurableJoint>();

        SwitchTypeEnum switch_type_enum = (SwitchTypeEnum)EditorGUILayout.EnumPopup("Тип концевика: ", switch_lever.TypeEnum);
        if (switch_lever.TypeEnum != switch_type_enum)
        {
            switch_lever.TypeEnum = switch_type_enum;
            switch_lever.ResetJoint();
            joint = switch_lever.Joint;
        }
        GUILayout.Space(10);

        switch (switch_lever.TypeEnum)
        {
            case SwitchTypeEnum.Plunger:

                SetLimits(joint, 0, 1, 0, 0, 0, 0);
                EditorGUILayout.LabelField("Настройки кнопки");
                switch_lever.ButtonDamper = EditorGUILayout.FloatField("Сила затухания: ", switch_lever.ButtonDamper);
                switch_lever.ButtonSpring = EditorGUILayout.FloatField("Сила пружины: ", switch_lever.ButtonSpring);
                switch_lever.MaxLength = EditorGUILayout.FloatField("Глубина вдавливания: ", switch_lever.MaxLength);
                switch_lever.HorButtonSlider = EditorGUILayout.Slider("Предел активации", switch_lever.HorButtonSlider, 0, 1);
                EditorGUILayout.HelpBox("Актуатор кнопочного типа, без рычага, срабатывает \nпри непосредственном воздействии на кнопку.", MessageType.Info);
                SetPlunger(switch_lever, joint);
                break;
            case SwitchTypeEnum.LeverRoller:

                SetLimits(joint, 0, 0, 0, 1, 0, 0);
                EditorGUILayout.LabelField("Настройки роликового рычага");
                switch_lever.AngularDamper = EditorGUILayout.FloatField("Сила затухания: ", switch_lever.AngularDamper);
                switch_lever.AngularSpring = EditorGUILayout.FloatField("Сила пружины: ", switch_lever.AngularSpring);
                switch_lever.MaxAngle = EditorGUILayout.Slider("Максимальный угол: ", switch_lever.MaxAngle, 0, 177);
                switch_lever.MinAngle = EditorGUILayout.Slider("Минимальный угол: ", switch_lever.MinAngle, 0, -177);
                switch_lever.HorLeverRollerSlider = EditorGUILayout.Slider("Предел активации", switch_lever.HorLeverRollerSlider, 0, 1);
                EditorGUILayout.HelpBox("Актуатор, когда он поворачивает, происходит \nзамыкание либо размыкание электрической цепи.", MessageType.Info);
                SetLeverRoller(switch_lever, joint);
                break;
            case SwitchTypeEnum.FlexibleRod:

                SetLimits(joint, 0, 0, 0, 1, 1, 1);

                break;
        }
        GUILayout.Space(10);

        EditorGUILayout.LabelField("Общие настройки");
        switch_lever.AnchorPos = EditorGUILayout.Vector3Field("Точка опоры: ", switch_lever.AnchorPos);
        joint.anchor = switch_lever.AnchorPos;
        GUILayout.Space(10);

        EditorGUILayout.LabelField("Настройки гизмо");
        float size_gizmo_sphere = EditorGUILayout.FloatField("Размер гизмо: ", switch_lever.SizeGizmoSphere);
        switch_lever.SizeGizmoSphere = size_gizmo_sphere < 0 ? 0 : size_gizmo_sphere;
    }

    private void SetLimits(ConfigurableJoint joint, int state_xMotion, int state_yMotion, int state_zMotion, int state_xAngular, int state_yAngular, int state_zAngular)
    {
        joint.xMotion = (ConfigurableJointMotion)state_xMotion;
        joint.yMotion = (ConfigurableJointMotion)state_yMotion;
        joint.zMotion = (ConfigurableJointMotion)state_zMotion;
        joint.angularXMotion = (ConfigurableJointMotion)state_xAngular;
        joint.angularYMotion = (ConfigurableJointMotion)state_yAngular;
        joint.angularZMotion = (ConfigurableJointMotion)state_zAngular;
    }

    private void SetPlunger(Switch switch_lever, ConfigurableJoint joint)
    {
        SoftJointLimit soft_limit = new SoftJointLimit();
        soft_limit.limit = switch_lever.MaxLength;
        JointDrive joint_drive = new JointDrive();
        joint_drive.positionSpring = switch_lever.ButtonSpring;
        joint_drive.positionDamper = switch_lever.ButtonDamper;
        joint_drive.maximumForce = float.MaxValue;
        joint.linearLimit = soft_limit;
        joint.yDrive = joint_drive;

        switch_lever.DoCalculations = () =>
        {
            float vec = switch_lever.Vector.y - switch_lever.transform.localPosition.y;

            if (switch_lever.MaxLength * switch_lever.HorButtonSlider < vec)
            {
                if (!switch_lever.MechState)
                {
                    switch_lever.SwitchOn();
                }
            }
            else
            {
                if (switch_lever.MechState)
                {
                    switch_lever.SwitchOff();
                }
            }
        };
    }

    private void SetLeverRoller(Switch switch_lever, ConfigurableJoint joint)
    {
        joint.highAngularXLimit = new SoftJointLimit() { bounciness = 0, contactDistance = 0, limit = switch_lever.MaxAngle };
        joint.lowAngularXLimit = new SoftJointLimit() { bounciness = 0, contactDistance = 0, limit = switch_lever.MinAngle };
        joint.angularXDrive = new JointDrive() { maximumForce = float.MaxValue, positionDamper = switch_lever.AngularDamper, positionSpring = switch_lever.AngularSpring };
    }
}