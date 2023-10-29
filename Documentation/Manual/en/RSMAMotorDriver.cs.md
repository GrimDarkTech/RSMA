# RSMAMotorDriver
[switch to API](../../../Documentation/ScriptingAPI/en/RSMAStepperMotor.cs.md)

Implements properties and functionality of electric motor driver

## Fields
| Field | Description | Type |
|--|--|--|
|connectedPin1|GPIO port pin connected to driver's 1st input|ConnectedPin|
|connectedPin2|GPIO port pin connected to driver's 2st input|ConnectedPin|
|connectedPinPWM|GPIO port pin connected to driver's PWM input|ConnectedPin|
|connectMicrocontroller|Microcontroller GPIO that connected to driver|RSMAGPIO|
## Methods
### float getOutput()
Returns output signal from driver
Returns: 
float value of output signal from driver
