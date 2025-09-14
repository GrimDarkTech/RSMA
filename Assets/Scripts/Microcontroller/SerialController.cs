using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class SerialController : RSMAMicrocontroller
{
    public static string serialPortName;

    private SerialPort serialPort = new SerialPort("COM1", 19200, Parity.None, 8, StopBits.One);
    private IEnumerator program;

    private IEnumerator MicroCycle()
    {

        while (true)
        {
            yield return new WaitForSeconds(0.1f);
            Debug.Log(serialPort.ReadLine());
        } 
    }

    private void Start()
    {
        serialPort.PortName= serialPortName;
        serialPort.ReadTimeout = 500;
        serialPort.WriteTimeout = 500;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopCoroutine(program);
        if (serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
    protected override void OnEnable()
    {
        GPIO.ResetAll();
        serialPort.Open();
        program = MicroCycle();
        StartCoroutine(program);
    }
}
