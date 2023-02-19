using UnityEngine;

public class RSMADataTransferSlave : MonoBehaviour
{
    protected string data;
    protected string deviceName;
    public  void SetDeviceName(string newDeviceName)
    {
        deviceName = newDeviceName;
    }
    public string GetDeviceName()
    {
        return deviceName;
    }
    public void SetData(string newData)
    {
        data = newData;
    }
    public string GetData()
    {
        return data;
    }
    public virtual string SendData()
    {
        return data;
    }
    public virtual string SendName()
    {
        return name;
    }
    public virtual void OnRequest()
    {

    }
    public virtual void ReciveData(string recivedData)
    {
        data = recivedData;
    }
}
