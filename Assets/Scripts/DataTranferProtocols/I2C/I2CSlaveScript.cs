using UnityEngine;

public class I2CSlaveScript : MonoBehaviour
{
    protected string data;
    protected string deviceName;
    public  void setDeviceName(string newDeviceName)
    {
        deviceName = newDeviceName;
    }
    public string getDeviceName()
    {
        return deviceName;
    }
    public void setData(string newData)
    {
        data = newData;
    }
    public string getData()
    {
        return data;
    }
    public virtual string sendData()
    {
        return data;
    }
    public virtual string sendName()
    {
        return name;
    }
    public virtual void onRequest()
    {

    }
    public virtual void reciveData(string recivedData)
    {
        data = recivedData;
    }
}
