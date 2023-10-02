using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Implements properties and functionality of master device in data transfer protocol
/// </summary>
public class RSMADataTransferMaster : MonoBehaviour
{
    /// <summary>
    /// List of devices connected to master device via the data bus
    /// </summary>
    public List<RSMADataTransferSlave> dataBus = new List<RSMADataTransferSlave>();

    protected string data;

    /// <summary>
    /// Sends data to target device
    /// </summary>
    /// <param name="targetAddress">Address of device connected to data bus</param>
    public virtual void SendData(int targetAddress)
    {
        RSMADataTransferSlave targetDevice;
        if (dataBus.Count > targetAddress && dataBus[targetAddress] != null)
        {
            targetDevice = dataBus[targetAddress];
            targetDevice.ReciveData(data);
        }
    }
    public virtual void ReciveData(int targetAddress)
    {
        RSMADataTransferSlave targetScript;
        if (dataBus.Count > targetAddress && dataBus[targetAddress] != null)
        {
            targetScript = dataBus[targetAddress];
            data = targetScript.SendData();
        }
        else
            Debug.Log("Error");
    }
    public virtual void Request(int targetAddress)
    {
        RSMADataTransferSlave targetScript;
            if (dataBus.Count > targetAddress && dataBus[targetAddress] != null)
            {
                targetScript = dataBus[targetAddress];
                targetScript.OnRequest();
            }
    }

    public virtual void RequestName(int targetAddress)
    {
        RSMADataTransferSlave targetScript;
            if (dataBus.Count > targetAddress && dataBus[targetAddress] != null)
            {
                targetScript = dataBus[targetAddress];
                data = targetScript.SendName();
            }
    }
}
