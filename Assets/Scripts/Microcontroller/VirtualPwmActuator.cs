using UnityEngine;
using RSMA.Microcontroller.Virtual;

[DisallowMultipleComponent]
public class VirtualPwmActuator : MonoBehaviour
{
    [SerializeField] private RSMAVirtualMicrocontroller microcontroller;
    [SerializeField] private VirtualActuatorKind kind = VirtualActuatorKind.Led;
    [SerializeField, Range(0, 255)] private int pwmPin;
    [SerializeField, Range(0, 255)] private int directionPin = 255;
    [SerializeField] private bool directionActiveHigh = true;
    [SerializeField] private bool brakeWhenDisabled;
    [SerializeField] private bool fault;
    [SerializeField] private float physicalMinimum;
    [SerializeField] private float physicalMaximum = 1f;
    [SerializeField, Min(0f)] private float slewRatePerSecond;

    private VirtualPwmActuatorModel model;

    protected virtual VirtualActuatorKind DefaultKind => VirtualActuatorKind.Led;
    protected virtual float DefaultPhysicalMinimum => physicalMinimum;
    protected virtual float DefaultPhysicalMaximum => physicalMaximum;

    public VirtualActuatorKind Kind => kind;
    public float EffectiveValue => model == null ? 0f : model.EffectiveValue;
    public float PhysicalValue => model == null ? physicalMinimum : model.PhysicalValue;
    public bool IsSafe => model == null || !model.Enabled && model.EffectiveValue == 0f;

    private void OnEnable()
    {
        if (microcontroller == null)
            microcontroller = GetComponentInParent<RSMAVirtualMicrocontroller>();
        if (kind == VirtualActuatorKind.Led && DefaultKind != VirtualActuatorKind.Led)
            kind = DefaultKind;
        model = new VirtualPwmActuatorModel(kind, DefaultPhysicalMinimum, DefaultPhysicalMaximum, slewRatePerSecond);
        if (microcontroller == null)
            enabled = false;
    }

    private void Update()
    {
        if (microcontroller == null || model == null)
            return;

        var pwm = microcontroller.GetPwmOutput((byte)pwmPin);
        var direction = 1;
        if (directionPin < 255)
        {
            var directionState = microcontroller.GetPinValue((byte)directionPin) >= 0.5f;
            if (!directionActiveHigh)
                directionState = !directionState;
            direction = directionState ? 1 : -1;
        }
        model.SetCommand(pwm.Duty, pwm.DigitalLevel, true, direction,
            brakeWhenDisabled && pwm.DutyCycle == 0, fault);
        model.AdvanceTo(microcontroller.SimulatedTimeMilliseconds);
    }

    private void OnDisable()
    {
        model?.SafeState();
    }

    public void SafeState()
    {
        model?.SafeState();
    }
}

public sealed class VirtualLedActuator : VirtualPwmActuator
{
    protected override VirtualActuatorKind DefaultKind => VirtualActuatorKind.Led;
}

public sealed class VirtualRelayActuator : VirtualPwmActuator
{
    protected override VirtualActuatorKind DefaultKind => VirtualActuatorKind.Relay;
}

public sealed class VirtualServoActuator : VirtualPwmActuator
{
    protected override VirtualActuatorKind DefaultKind => VirtualActuatorKind.Servo;
    protected override float DefaultPhysicalMaximum => 180f;
}

public sealed class VirtualDcMotorActuator : VirtualPwmActuator
{
    protected override VirtualActuatorKind DefaultKind => VirtualActuatorKind.DcMotor;
    protected override float DefaultPhysicalMinimum => -1f;
    protected override float DefaultPhysicalMaximum => 1f;
}