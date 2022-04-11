using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyboardInputScript : I2CSlaveScript
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
                data = ""+eventKey.keyCode;
                isTime = false;
                StartCoroutine(keyTimer());
            }
        }
    }
    private IEnumerator keyTimer()
    {
        
        yield return new WaitForSeconds(1/frequency);
        isTime = true;
    }
    public override string sendData()
    {
        return (data);
    }
}
