
public class SerialController : RSMAMicrocontroller
{



    protected override void OnDisable()
    {
        base.OnDisable();
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        GPIO.ResetAll();
    }
}
