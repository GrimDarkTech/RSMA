using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class WheelSetUp : MonoBehaviour
{
    public GameObject MiniWheel;

    [Range(0, 360)] public float Angle;
    [Min(0)] public int Count;
    [Min(0)] public float Size;
    [Min(0)] public float Range;

    private int _count;
    private float _size;
    private float _range;
    private float _angle;

    public void Update()
    {
        if (!MiniWheel || Count == 0)
        {
            ClearWheel();
            return;
        }
        if (_count != Count) SetUp();
        if (_range != Range) SetRange();
        if (_size != Size) SetSize();
        if (_angle != Angle) SetAngle();

        return;
    }

    private void SetAngle()
    {
        List<GameObject> wheels = GetWheels();
        foreach (var wheel in wheels)
        {
            wheel.transform.rotation = Quaternion.LookRotation((wheel.transform.position- transform.position).normalized, transform.forward);
            wheel.transform.Rotate(0, 0, Angle, Space.Self);
        }

        _angle = Angle;
    }

    private void SetRange()
    {
        List<GameObject> wheels = GetWheels();

        for (int i = 0; i < wheels.Count; i++)
        {
            wheels[i].transform.localPosition = Vector3.zero;
            wheels[i].transform.Translate(transform.forward * Range, Space.Self);
        }

        _range = Range;
    }

    private void SetSize()
    {
        List<GameObject> wheels = GetWheels();
        foreach (var wheel in wheels)
        {
            wheel.transform.localScale = new Vector3(Size, Size, Size);
        }

        _size = Size;
    }

    private void SetUp()
    {
        ClearWheel();

        float angle_step = 360f / Count;
        for (int i = 0; i < Count; i++)
        {
            GameObject wheel_obj = Instantiate(MiniWheel, transform.parent);
            wheel_obj.name = MiniWheel.name;

            AddJoints(wheel_obj);

            float angle = angle_step * (i+1);

            wheel_obj.transform.localPosition += wheel_obj.transform.forward * 2;
            wheel_obj.transform.RotateAround(transform.position, transform.up, angle);
        }

        SetRange();
        SetSize();
        SetAngle();

        _count = Count;
    }

    private void ClearWheel()
    {
        List<GameObject> wheels = GetWheels();
        int wheel_count = wheels.Count;

        for (int i = wheel_count-1; i >= 0; i--)
        {
            DestroyImmediate(wheels[i]);
        }

        wheels.Clear();
    }

    private List<GameObject> GetWheels()
    {
        List<GameObject> result = new List<GameObject>();

        int child_count = transform.parent.childCount;
        for (int index = 0; index < child_count; index++)
        {
            Transform obj_transform = transform.parent.GetChild(index);
            string obj_name = obj_transform.name;

            if (obj_name != MiniWheel.name) continue;
            result.Add(obj_transform.gameObject);
        }

        return result;
    }

    private void AddJoints(GameObject wheel)
    {
        if (!wheel.GetComponent<Rigidbody>())
        {
            wheel.AddComponent<Rigidbody>();
        }

        if (!wheel.GetComponent<HingeJoint>())
        {
            wheel.AddComponent<HingeJoint>();
        }

        HingeJoint wheel_joint = wheel.GetComponent<HingeJoint>();
        wheel_joint.connectedBody = GetComponent<Rigidbody>();
        wheel_joint.anchor = wheel.transform.up;
        wheel_joint.axis = wheel.transform.up;
    }
}
