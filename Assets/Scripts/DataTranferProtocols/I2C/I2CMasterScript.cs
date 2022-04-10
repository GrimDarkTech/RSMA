using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class I2CMasterScript : MonoBehaviour
{
    public List<I2CSlaveScript> busI2C = new List<I2CSlaveScript>();
    private string data;
    public void setData(string newData) 
    {
        data = newData;
    }
    public string getData()
    {
        return data;
    }
    public virtual void  sendData(int targetAddress)
    {
        I2CSlaveScript targetScript;
        if (busI2C.Count > targetAddress && busI2C[targetAddress] != null)
        {
            targetScript = busI2C[targetAddress];
            targetScript.reciveData(data);
        }
    }
    public virtual void reciveData(int targetAddress)
    {
        I2CSlaveScript targetScript;
        if (busI2C.Count > targetAddress && busI2C[targetAddress] != null)
        {
            targetScript = busI2C[targetAddress];
            data = targetScript.sendData();
        }
        else
            Debug.Log("Error");
    }
    public virtual void request(int targetAddress)
    {
            I2CSlaveScript targetScript;
            if (busI2C.Count > targetAddress && busI2C[targetAddress] != null)
            {
                targetScript = busI2C[targetAddress];
                targetScript.onRequest();
            }
    }

    public virtual void requestName(int targetAddress)
    {
        I2CSlaveScript targetScript;
            if (busI2C.Count > targetAddress && busI2C[targetAddress] != null)
            {
                targetScript = busI2C[targetAddress];
                data = targetScript.sendName();
            }
    }
}
