using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RSMAEncoder : RSMADataTransferSlave
{
    public float encoderResolution = 1;

    public GameObject rotor;
    public HingeJoint motor;

    void FixedUpdate()
    {
        float current_angle = motor.angle;
        float angular_velocity = motor.velocity;

        if (motor.angle < 0)
        {
            current_angle = 360 + motor.angle;
        }
        data = $"{angular_velocity};{current_angle}";
        Debug.Log(data);
    }
    public override string SendData()
    {
        return (data);
    }
}
