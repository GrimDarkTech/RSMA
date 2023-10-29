# RSMAEncoder
[switch to API](../../../Documentation/ScriptingAPI/en/AxisEnum.cs.md)



## Fields
| Field | Description | Type |
|--|--|--|
|Distance|the distance can be negative and positiveAt what distance from the object the encoder will be located|float|
|EncoderResolution|shaft or bore.output by the encoder during one 360 degree revolution of the encoderEncoder resolution is the number of pulses per revolution (PPR) or bits|float|
|Shaft|the object of the shaft that will spin together with the motor|GameObject|
|Motor|Motor of the object under study|HingeJoint|
## Properties
| Property | Description | Type |
|--|--|--|
|EncoderTypeObj|Defines the type of encoder used in the simulation|EncoderType|
|EncoderRangeType|Allows you to set up a limit on the rotation of the shaft|EncoderRangeType|
|AxisDirection|Determines which side the encoder will be located relative to the motor|AxisEnum|
|OutPut|Determines how much data the encoder transmits over the data transmitter|EncoderOutput|
|AutoAxis|to the motorAutomatic determination of the axis from which the encoder will be connected|bool|
|EncoderRangeType|limits of the rotation|enum|
## Methods
### float GetVelocity()
Get the speed of rotation of the shaft in degrees per second
Returns: 

### int GetSide()
2 - left side1 - right side0 - no sideGet the direction of the shaft
Returns: 

### string GetHZ()
Get the rotation frequency of the encoder shaft
### string GetMeasureAngle()
updates the dataGet the calculated step angle with which the encoder
Returns: 

### float GetAngle()
depends on the type of encoderGet the angle of rotation of the encoder shaft, which
# RSMAEncoderEditor
[switch to API](../../../Documentation/ScriptingAPI/en/AxisEnum.cs.md)

Deleted in the final buildSettings for the unique display of encoder data.

