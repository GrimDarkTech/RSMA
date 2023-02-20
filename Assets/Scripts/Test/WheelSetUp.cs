using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class WheelSetUp : MonoBehaviour
{
    public GameObject Wheel;
    public GameObject MiniWheel;

    [Range(0, 360)] public float Angle;
    [Min(0)] public int Count;
    [Min(0)] public float Size;
    [Min(0)] public float Range;

    private GameObject _check_wheel;
    private GameObject _check_mini_wheel;
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

        if (_check_mini_wheel == null)
        {
            _check_mini_wheel = MiniWheel;
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

        if (_check_wheel != Wheel || _check_mini_wheel != MiniWheel)
        {
            ClearWheel();
            _check_wheel = Wheel;
            _check_mini_wheel = MiniWheel;
            SetUp();
        }

        if (_count != Count || _count == 0) SetUp();
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

        float angle_step = 360f / _count;
        for (int i = 0; i < wheels.Count; i++)
        {
            GameObject wheel_obj = wheels[i];
            wheel_obj.transform.rotation = _wheel.transform.rotation;
            wheel_obj.transform.position = _wheel.transform.position + _wheel.transform.right * Range;
            wheel_obj.transform.RotateAround(_wheel.transform.position, _wheel.transform.up, angle_step * (i+1));
        }
        SetAngle();

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

    private Vector3 GetDirectVector(Vector3 side_vector, GameObject obj)
    {
        if (side_vector.x >= side_vector.y && side_vector.x >= side_vector.z)
        {
            return obj.transform.right;
        }

        if (side_vector.y >= side_vector.x && side_vector.y >= side_vector.z)
        {
            return obj.transform.up;
        }

        if (side_vector.z >= side_vector.x && side_vector.z >= side_vector.y)
        {
            return obj.transform.forward;
        }

        return Vector3.zero;
    }
}
