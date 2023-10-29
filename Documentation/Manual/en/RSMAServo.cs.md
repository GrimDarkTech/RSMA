# RSMAServo
[switch to API](../../../Documentation/ScriptingAPI/en/RSMAMotor.cs.md)

Implements properties and functionality of servo motor

## Fields
| Field | Description | Type |
|--|--|--|
|rotor|Body used as a motor rotor|Rigidbody|
|startMotorAxis|Represents the motor axis, emanating from origin|Vector3|
|connectMicrocontroller|Microcontroller GPIO connected to servo|RSMAGPIO|
|limits|Angles|Vector2|
|connectedPin|Microcontroller GPIO port pin connected to servo|ConnectedPin|
|torque||Maximum|
|torque|Maximum torque|float|
|maxAngle|Maximum angle|float|
|damper|Damper factor|float|
|isUseLimit|Is servo has limits|bool|
## Methods
### void MotorInit()
Inits motor and sets up motor component
Returns: 

### void SetMotorAxis(Vector3 newMotorAxis)

Returns: 

### Vector3 GetMotorAxis()

Returns: 

### void SetMotorAxis(float newMotorX, float newMotorY, float newMotorZ)

Returns: 

### void SetMotorAnchor(Vector3 newMotorAnchor)

Returns: 

### void SetSpringActive(bool isSpringActive)

Returns: 

### void SetLimitsActive(bool isLimitsActive)

Returns: 

### void SetRotor(Rigidbody newRotor)

Returns: 

### void SetTargetAngle(float targetAngle)

Returns: 

### void SetTorque(float newTorque)

Returns: 

### void SetDamper(float newDamper)

Returns: 

### void SetLimits()

Returns: 

