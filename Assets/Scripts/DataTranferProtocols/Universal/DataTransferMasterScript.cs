using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataTransferMasterScript : MonoBehaviour
{
    public List<DataTransgerSlaveScript> dataBus = new List<DataTransgerSlaveScript>();
    protected string data;
    public void SetData(string newData) 
    {
        data = newData;
    }
    public string GetData()
    {
        return data;
    }
    public virtual void  SendData(int targetAddress)
    {
        DataTransgerSlaveScript targetScript;
        if (dataBus.Count > targetAddress && dataBus[targetAddress] != null)
        {
            targetScript = dataBus[targetAddress];
            targetScript.ReciveData(data);
        }
    }
    public virtual void ReciveData(int targetAddress)
    {
        DataTransgerSlaveScript targetScript;
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
        DataTransgerSlaveScript targetScript;
            if (dataBus.Count > targetAddress && dataBus[targetAddress] != null)
            {
                targetScript = dataBus[targetAddress];
                targetScript.OnRequest();
            }
    }

    public virtual void RequestName(int targetAddress)
    {
        DataTransgerSlaveScript targetScript;
            if (dataBus.Count > targetAddress && dataBus[targetAddress] != null)
            {
                targetScript = dataBus[targetAddress];
                data = targetScript.SendName();
            }
    }
}
