# RSMAMotor 
[switch to API](Documentation/API/en/RSMAMotor.md)

Implements properties and functionality of DC electric motor

## Properties
|Property|Function|Type|
|--|--|--|
|Rotor|Body used as a motor rotor|Rigidbody|
|motorDriver|Driver connected to motor|RSMAMotorDriver|
|motorAxis|Represents the motor axis, emanating from origin|Vector3|
|isResetMotorAnchor|Resets MotorAnchor with startMotorAnchor|bool|
|motorAnchor|Represents the Motor Anchor|Vector3|
|connectedAnchor|Represents the anchor for connected body|Vector3|
|torque|Maximum torque of motor|float|
|angularVelocity|Maximum angle velocity of motor in degrees per second|float|
