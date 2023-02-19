using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSMAGamepad : RSMADataTransferSlave
{
    bool isTime = true;
    public float delay = 0.01f;
    private void Update()
    {
         if (isTime)
         {
            data = Input.GetAxis("Horizontal").ToString()+" "+ Input.GetAxis("Vertical");
            isTime = false;
            StartCoroutine(KeyTimer());
         }
    }

    private IEnumerator KeyTimer()
    {

        yield return new WaitForSeconds(delay);
        isTime = true;
    }
    public override string SendData()
    {
        return (data);
    }
    private void Start()
    {
        data = "0 0";
    }
}
