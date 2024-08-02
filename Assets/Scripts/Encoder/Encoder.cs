using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Encoder : RSMADataTransferSlave
{
    public RSMA.EncoderType type;

    public int resolution = 1;
    /// <summary>
    /// Body used as a motor rotor
    /// </summary>
    public GameObject connectedBody;
    /// <summary>
    /// Represents the encoder axis, emanating from origin
    /// </summary>
    public Vector3 axis;

    private int _counter;

    private void Start()
    {
        if(type == RSMA.EncoderType.Incremental)
        {
            _counter = 0;
        }
        else
        {
            Vector3 euler = connectedBody.transform.rotation.eulerAngles;
            float angle = Vector3.Dot(euler, axis);
            _counter = (int)(angle / 360 * resolution);
        }
    }

    public override string SendData()
    {
        Vector3 euler = connectedBody.transform.localRotation.eulerAngles;
        float angle = Vector3.Dot(euler, axis);
        int current = (int)(angle / 360 * resolution);

        return (current - _counter).ToString();
    }

}

namespace RSMA
{
    public enum EncoderType
    {
        Incremental = 0,
        Absolute = 1
    }
}

