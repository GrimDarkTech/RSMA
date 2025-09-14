using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Class for creating and configuring omnidirectional wheels
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Mechanics/Setting_up_omnidirectional_wheels.md")]
public class OmnidirectionalWheel : MonoBehaviour
{
    /// <summary>
    /// Wheel object used as the basis for an omnidirectional wheel
    /// </summary>
    public GameObject wheel;
    /// <summary>
    /// Rotation of the wheel object
    /// </summary>
    public Quaternion wheelRotation;


    [Space(10)]

    /// <summary>
    /// Roller object used to form an omnidirectional wheel
    /// </summary>
    public GameObject roller;
    /// <summary>
    /// The number of rollers mounted on the wheel
    /// </summary>
    [Min(1)]
    public int rollerCount = 1;
    /// <summary>
    /// The radius of installation of the rollers
    /// </summary>
    [Min(0)] 
    public float installationRadius = 0f;
    /// <summary>
    /// Additional rotation of the rollers object
    /// </summary>
    public Quaternion additionalRollerRotation;
    /// <summary>
    /// The plane of rotation of the wheel in the local coordinate system
    /// </summary>
    public CoordinatePlane wheelPlane = CoordinatePlane.yz;
    /// <summary>
    /// The angle of inclination of the rollers relative to the circumference of the wheel
    /// </summary>
    [Range(0f, 360f)]
    public float rollerAngle = 0f;
    /// <summary>
    /// The axis relative to which the roller is rotated to the wheel plane
    /// </summary>
    public CoordinateAxis rollerRotationAxis = CoordinateAxis.x;

    [Space(10)]

    /// <summary>
    /// The axis of rotation of the roller
    /// </summary>
    public CoordinateAxis rollerHingeAxis = CoordinateAxis.y;

    /// <summary>
    /// Creates a wheel with the specified parameters on the attached object
    /// </summary>
    [ContextMenu("Form wheel")]
    public void FormWheel()
    {
        if (wheel != null && roller != null)
        {
            int childCount = gameObject.transform.childCount;

            while (gameObject.transform.childCount > 0)
            {
                DestroyImmediate(gameObject.transform.GetChild(0).gameObject);
            }

            GameObject newWheel = Instantiate(wheel);
            newWheel.transform.parent = gameObject.transform;
            newWheel.transform.localPosition = Vector3.zero;
            newWheel.transform.localRotation = wheelRotation;

            Rigidbody wheelRigidbody = newWheel.GetComponent<Rigidbody>();

            if (wheelRigidbody == null)
            {
                wheelRigidbody = newWheel.AddComponent<Rigidbody>();
            }

            float deltaAngle = 360f / rollerCount;

            for(int i = 0; i < rollerCount; i++)
            {
                GameObject newRoller = Instantiate(roller);
                newRoller.transform.parent = gameObject.transform;
                newRoller.transform.position = gameObject.transform.position + GetVectorFromPlane(gameObject.transform, wheelPlane, deltaAngle * i) * installationRadius;
                newRoller.transform.rotation = additionalRollerRotation;
                RotateInPlane(newRoller, gameObject.transform, wheelPlane, deltaAngle * i);
                RotateAroundLocalAxis(newRoller, rollerRotationAxis, rollerAngle);

                HingeJoint rollerJoint = newRoller.AddComponent<HingeJoint>();
                rollerJoint.axis = AxisToLocalVector(newRoller, rollerHingeAxis);
                rollerJoint.connectedBody = wheelRigidbody;
            }
        }
        else
        {
            Debug.LogError($"Wheel or roller object in {gameObject.name} is null");
        }
    }

    private Vector3 GetVectorFromPlane(Transform coordinateSystem, CoordinatePlane plane, float angle)
    {
        Vector3 output = new Vector3();

        if (plane == CoordinatePlane.zx)
        {
            output = Quaternion.Euler(0, angle, 0) * coordinateSystem.forward;
        }
        else if (plane == CoordinatePlane.yz)
        {
            output = Quaternion.Euler(angle, 0, 0) * coordinateSystem.up;
        }
        else if (plane == CoordinatePlane.xy)
        {
            output = Quaternion.Euler(0, 0, angle) * coordinateSystem.right;
        }
        
        return output;
    }
    private void RotateInPlane(GameObject target, Transform coordinateSystem, CoordinatePlane plane, float angle)
    {
        Vector3 direction = new Vector3();

        if (plane == CoordinatePlane.zx)
        {
            direction = coordinateSystem.up;
        }
        else if (plane == CoordinatePlane.yz)
        {
            direction = coordinateSystem.right;
        }
        else if (plane == CoordinatePlane.xy)
        {
            direction = coordinateSystem.forward;
        }

        target.transform.Rotate(direction, angle);
    }
    private void RotateAroundLocalAxis(GameObject target, CoordinateAxis axis, float angle)
    {
        Vector3 dir = AxisToLocalVector(target, axis);
        target.transform.Rotate(dir, angle);
    }
    private Vector3 AxisToLocalVector(GameObject target, CoordinateAxis axis)
    {
        Vector3 direction = new Vector3();

        if (axis == CoordinateAxis.x)
        {
            direction = new Vector3(1, 0, 0);
        }
        else if (axis == CoordinateAxis.y)
        {
            direction = new Vector3(0, 1, 0);
        }
        else if (axis == CoordinateAxis.z)
        {
            direction = new Vector3(0, 0, 1);
        }

        return direction;
    }
}

[Serializable]
public enum CoordinatePlane
{
    zx,
    yz,
    xy
}
public enum CoordinateAxis
{
    x,
    y,
    z
}

#if UNITY_EDITOR

[CustomEditor(typeof(OmnidirectionalWheel))]
public class WheelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        OmnidirectionalWheel wheel = (OmnidirectionalWheel)target;

        DrawDefaultInspector();

        if (GUILayout.Button("Form wheel"))
        {
            wheel.FormWheel();
        }
    }
}

#endif