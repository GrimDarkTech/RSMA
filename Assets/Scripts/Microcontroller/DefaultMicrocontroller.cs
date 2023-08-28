using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefaultMicrocontroller : RSMAMicrocontroller
{
    [SerializeField] private RSMALED powerLed;
    protected override void OnDisable()
    {
        powerLed.SetMode(0);
        base.OnDisable();
    }
    protected override void OnEnable()
    {
        powerLed.SetMode(1);
        base.OnEnable();
        GPIO.ResetAll();
    }
}
