using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[ExecuteInEditMode]
public class WheelSetUp : MonoBehaviour
{
    public GameObject Wheel;
    public BetterVector WheelDirections;

    public GameObject MiniWheel;
    public BetterVector RollersDirections;

    [Range(0, 360)] public float Angle;
    [Min(0)] public int RollerCount;
    [Min(0)] public float RollerSize;
    [Min(0)] public float Radius;

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
            for (int i = child_count - 1; i >= 0; i--)
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

        if (_count != RollerCount || _count == 0) SetUp();
        if (_range != Radius) SetRange();
        if (_size != RollerSize) SetSize();
        if (_angle != Angle) SetAngle();

        return;
    }

    private void SetAngle()
    {
        List<GameObject> wheels = GetWheels();
        foreach (var wheel in wheels)
        {
            wheel.transform.rotation = Quaternion.LookRotation((wheel.transform.position - _wheel.transform.position).normalized, GetDirectVector(WheelDirections, _wheel, VecEnum.forward));
            wheel.transform.Rotate(0, 0, Angle, Space.Self);
        }

        _angle = Angle;
    }

    private void SetRange()
    {
        List<GameObject> wheels = GetWheels();

        float angle_step = 360f / wheels.Count;
        for (int i = 0; i < wheels.Count; i++)
        {
            GameObject wheel_obj = wheels[i];
            wheel_obj.transform.rotation = _wheel.transform.rotation;
            wheel_obj.transform.position = _wheel.transform.position + GetDirectVector(WheelDirections, _wheel, VecEnum.right) * Radius;
            wheel_obj.transform.RotateAround(_wheel.transform.position, GetDirectVector(WheelDirections, _wheel, VecEnum.up), angle_step * i);
        }
        SetAngle();

        _range = Radius;
    }

    private void SetSize()
    {
        List<GameObject> wheels = GetWheels();
        foreach (var wheel in wheels)
        {
            wheel.transform.localScale = new Vector3(RollerSize, RollerSize, RollerSize);
        }

        _size = RollerSize;
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

        for (int i = 0; i < RollerCount; i++)
        {
            GameObject wheel_obj = Instantiate(MiniWheel, transform);
            wheel_obj.name = MiniWheel.name;

            AddJoints(wheel_obj);

            HingeJoint joint = wheel_obj.GetComponent<HingeJoint>();
        }

        SetRange();
        SetSize();
        SetAngle();
        _count = RollerCount;
    }

    private void ClearWheel()
    {
        int child_count = transform.childCount;
        for (int index = child_count - 1; index >= 0; index--)
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
    }

    private Vector3 GetDirectVector(BetterVector side_vector, GameObject obj, VecEnum required)
    {
        if (required == VecEnum.forward)
        {
            if (side_vector.forward == VecEnum.forward) return obj.transform.forward;
            if (side_vector.forward == VecEnum.right) return obj.transform.right;
            if (side_vector.forward == VecEnum.up) return obj.transform.up;
        }

        if (required == VecEnum.right)
        {
            if (side_vector.right == VecEnum.forward) return obj.transform.forward;
            if (side_vector.right == VecEnum.right) return obj.transform.right;
            if (side_vector.right == VecEnum.up) return obj.transform.up;
        }

        if (required == VecEnum.up)
        {
            if (side_vector.up == VecEnum.forward) return obj.transform.forward;
            if (side_vector.up == VecEnum.right) return obj.transform.right;
            if (side_vector.up == VecEnum.up) return obj.transform.up;
        }

        return Vector3.zero;
    }
}

[Serializable]
public struct BetterVector
{
    public VecEnum right;
    public VecEnum forward;
    public VecEnum up;
}
