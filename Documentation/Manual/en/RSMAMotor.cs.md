# RSMAMotor
[switch to API](../../../Documentation/ScriptingAPI/en/SerialController.cs.md)

Implements properties and functionality of DC electric motor

## Fields
| Field | Description | Type |
|--|--|--|
|rotor|Body used as a motor rotor|Rigidbody|
|motorDriver|Driver connected to motor|RSMAMotorDriver|
|motorAxis|Represents the motor axis, emanating from origin|Vector3|
|isResetMotorAnchor|Resets MotorAnchor with startMotorAnchor|bool|
|motorAnchor|Represents the Motor Anchor|Vector3|
|connectedAnchor|Represents the anchor for connected body|Vector3|
|torque||Maximum|
|torque|Maximum torque of motor|float|
|angularVelocity|Maximum angle velocity of motor in degrees per second|float|
