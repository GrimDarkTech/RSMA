using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyboardInput : DataTransgerSlaveScript
{
    bool isTime = true;
    public float frequency = 1;
    void OnGUI()
    {
        Event eventKey = Event.current;
        if (isTime)
        {
            if (eventKey.isKey)
            {
                data = "" + eventKey.keyCode;
                isTime = false;
                StartCoroutine(KeyTimer());
            }
            else
                data = "";
        }
    }
    private IEnumerator KeyTimer()
    {
        
        yield return new WaitForSeconds(1/frequency);
        isTime = true;
    }
    public override string SendData()
    {
        return (data);
    }
}
