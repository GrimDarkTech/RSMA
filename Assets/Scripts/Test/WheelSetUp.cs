using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class WheelSetUp : MonoBehaviour
{
    public GameObject Wheel;
    public GameObject MiniWheel;

    [Range(0, 360)] public float Angle;
    [Min(0)] public int Count;
    [Min(0)] public float Size;
    [Min(0)] public float Range;

    private GameObject _check_wheel;

    private GameObject _wheel;
    private int _count;
    private float _size;
    private float _range;
    private float _angle;

    public void Update()
    {
        if (Application.isPlaying) return;

        if (_check_wheel == null)
        {
            _check_wheel = Wheel;
        }

        if (!Wheel || !MiniWheel)
        {
            int child_count = transform.childCount;
            for (int i = child_count-1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            return;
        }

        if (_check_wheel != Wheel)
        {
            ClearWheel();
            _check_wheel = Wheel;
            _count = 0;
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
            wheel.transform.rotation = Quaternion.LookRotation((wheel.transform.position- _wheel.transform.position).normalized, _wheel.transform.forward);
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
            wheels[i].transform.Translate(_wheel.transform.forward * Range, Space.Self);
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

        _wheel = Instantiate(Wheel, transform);
        if (!_wheel.GetComponent<Rigidbody>())
        {
            _wheel.AddComponent<Rigidbody>();
        }
        _wheel.name = Wheel.name;

        for (int i = 0; i < Count; i++)
        {
            GameObject wheel_obj = Instantiate(MiniWheel, transform);
            wheel_obj.name = MiniWheel.name;

            AddJoints(wheel_obj);

            float angle = angle_step * (i+1);

            wheel_obj.transform.localPosition += wheel_obj.transform.forward * 2;
            wheel_obj.transform.RotateAround(_wheel.transform.position, _wheel.transform.up, angle);
        }

        SetRange();
        SetSize();
        SetAngle();

        _count = Count;
    }

    private void ClearWheel()
    {
        int child_count = transform.childCount;
        for (int index = child_count-1; index >= 0; index--)
        {
            DestroyImmediate(transform.GetChild(index).gameObject);
        }
    }

    private List<GameObject> GetBigWheels()
    {
        List<GameObject> result = new List<GameObject>();

        int child_count = transform.childCount;
        for (int index = 0; index < child_count; index++)
        {
            Transform obj_transform = transform.GetChild(index);
            string obj_name = obj_transform.name;

            if (obj_name != Wheel.name) continue;
            result.Add(obj_transform.gameObject);
        }

        return result;
    }

    private List<GameObject> GetWheels()
    {
        List<GameObject> result = new List<GameObject>();

        int child_count = transform.childCount;
        for (int index = 0; index < child_count; index++)
        {
            Transform obj_transform = transform.GetChild(index);
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
        wheel_joint.connectedBody = _wheel.GetComponent<Rigidbody>();
        wheel_joint.anchor = wheel.transform.up;
        wheel_joint.axis = wheel.transform.up;
    }
}
