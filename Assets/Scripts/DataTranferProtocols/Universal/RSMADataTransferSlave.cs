using UnityEngine;
/// <summary>
/// Implements properties and functionality of slave device in data transfer protocol
/// </summary>
public class RSMADataTransferSlave : MonoBehaviour
{
    protected string data;
    protected string deviceName;

    /// <summary>
    /// Sets device name
    /// </summary>
    /// <param name="deviceName">Device name</param>
    public  void SetDeviceName(string deviceName)
    {
       this.deviceName = deviceName;
    }
    /// <summary>
    /// Gets device name
    /// </summary>
    /// <returns>Device name</returns>
    public string GetDeviceName()
    {
        return deviceName;
    }
    /// <summary>
    /// Returns data
    /// </summary>
    /// <returns>Data</returns>
    public virtual string SendData()
    {
        return data;
    }
    /// <summary>
    /// Returns device name
    /// </summary>
    /// <returns></returns>
    public virtual string SendName()
    {
        return name;
    }
    /// <summary>
    /// Called when master device requests
    /// </summary>
    public virtual void OnRequest()
    {

    }
    /// <summary>
    /// Recives data from data bus
    /// </summary>
    /// <param name="recivedData">Data to recive</param>
    public virtual void ReciveData(string recivedData)
    {
        data = recivedData;
    }
}
