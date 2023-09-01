using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(HingeJoint))]
public class LeverRoller : MonoBehaviour
{
    public Vector3 AnchorPostion;
    public bool IsActivatedOnBaseCollider;
    public float ActivationAngle;
    public float GizmoSize = 0.01f;

    public UnityEvent onPressed;
    public UnityEvent onReleased;

    private HingeJoint Joint;
    private bool _isPressed;

    void Start()
    {
        Joint = GetComponent<HingeJoint>();
        if (Joint == null)
        {
            Debug.LogError($"Не удалось получить компонент {typeof(HingeJoint)} у объекта");
        }
    }

    void Update()
    {
        if (Joint.angle > ActivationAngle)
        {
            if (_isPressed) return;
            Pressed();
        }
        else
        {
            if (!_isPressed) return;
            Released();
        }
    }

    private void Pressed()
    {
        _isPressed = true;
        onPressed.Invoke();
        Debug.Log("Pressed");
    }

    private void Released()
    {
        _isPressed = false;
        onReleased.Invoke();
        Debug.Log("Released");
    }

    public void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;


        Vector3 point = 
              AnchorPostion.x * transform.right * transform.localScale.x 
            + AnchorPostion.y * transform.up * transform.localScale.y
            + AnchorPostion.z * transform.forward * transform.localScale.z
            + transform.position;

        Gizmos.DrawSphere(point, GizmoSize);
    }
}

[CustomEditor(typeof(LeverRoller))]
public class LeverRollerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LeverRoller lever = (LeverRoller)target;
        HingeJoint joint = lever.GetComponent<HingeJoint>();

        bool value = ColliderCheck(lever, joint);

        if (value != true)
        {
            MaxAngle(lever, joint);
        }
        AnchorPoint(lever, joint);
    }

    private bool ColliderCheck(LeverRoller lever, HingeJoint joint)
    {
        bool activation_state = EditorGUILayout.Toggle("Активация при косании: ", lever.IsActivatedOnBaseCollider, GUILayout.ExpandHeight(true));

        if (activation_state == true && joint.connectedBody == null)
        {
            activation_state = false;
            lever.IsActivatedOnBaseCollider = activation_state;
            Debug.LogWarning("Не указан объект connectedBody");
            return activation_state;
        }
        lever.IsActivatedOnBaseCollider = activation_state;
        return activation_state;
    }

    private void MaxAngle(LeverRoller lever, HingeJoint joint)
    {
        JointLimits joint_limit = new JointLimits();
        joint_limit.max = lever.ActivationAngle;
        joint_limit.min = -10;

        float angle = EditorGUILayout.FloatField("Угол активации: ", lever.ActivationAngle);

        if (angle < 0)
        {
            angle = 0;
        }

        lever.ActivationAngle = angle;
        joint.limits = joint_limit;
    }

    private void AnchorPoint(LeverRoller lever, HingeJoint joint)
    {
        lever.AnchorPostion = EditorGUILayout.Vector3Field("Точка опоры: ", lever.AnchorPostion);
        float gizmoSize = EditorGUILayout.FloatField("Размер Gizmo: ", lever.GizmoSize);
        if (gizmoSize < 0) gizmoSize = 0;

        lever.GizmoSize = gizmoSize;
        joint.anchor = lever.AnchorPostion;
    }
}
