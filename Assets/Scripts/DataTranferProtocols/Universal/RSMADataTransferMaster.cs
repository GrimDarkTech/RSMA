using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSMADataTransferMaster : MonoBehaviour
{
    public List<RSMADataTransferSlave> dataBus = new List<RSMADataTransferSlave>();
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
        RSMADataTransferSlave targetScript;
        if (dataBus.Count > targetAddress && dataBus[targetAddress] != null)
        {
            targetScript = dataBus[targetAddress];
            targetScript.ReciveData(data);
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
