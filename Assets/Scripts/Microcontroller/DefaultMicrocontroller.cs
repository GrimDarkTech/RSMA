using UnityEngine;


/// <summary>
/// Implements the properties and behaviors of AVR/STM-like microcontrollers. When turned on, the PowerLED LED activates
/// </summary>
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
