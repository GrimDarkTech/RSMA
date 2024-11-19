using UnityEngine;


/// <summary>
/// Simulates the operation of an absolute and incremental encoder with a built-in pulse counter.
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Electronics/Setting_up_encoders.md")]
public class Encoder : RSMADataTransferSlave
{
    /// <summary>
    /// Type of device being modeled
    /// </summary>
    public RSMA.EncoderType type;
    /// <summary>
    /// The resolution of the encoder. Number of pulses per revolution
    /// </summary>
    public int resolution = 1;
    /// <summary>
    /// Body connected to encoder shaft
    /// </summary>
    public GameObject connectedBody;
    /// <summary>
    /// Represents the encoder axis, emanating from origin
    /// </summary>
    public Vector3 axis;

    private Vector3 _currentAngles;

    [SerializeField]
    private float _measuredAngles;

    private void Start()
    {
        if (type == RSMA.EncoderType.Incremental)
        {
            _measuredAngles = 0f;
        }
        else
        {
            _measuredAngles = Vector3.Dot(connectedBody.transform.eulerAngles, axis);
        }

        _currentAngles = connectedBody.transform.eulerAngles;
    }

    private void Update()
    {
        float delta = Vector3.Dot(connectedBody.transform.eulerAngles - _currentAngles, axis);

        if (delta > 180)
        {
            delta -= 360;
        }
        else if (delta < -180)
        {
            delta += 360;
        }

        _measuredAngles += delta;
        _currentAngles = connectedBody.transform.eulerAngles;
    }
    /// <summary>
    /// Sends the number of pulses measured by the encoder
    /// </summary>
    /// <returns>Number of pulses measured by the encoder counter</returns>
    public override string SendData()
    {
        int counter = (int)(_measuredAngles / 360 * resolution);

        return counter.ToString();
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

