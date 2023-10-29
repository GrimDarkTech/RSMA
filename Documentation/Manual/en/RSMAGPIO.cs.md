# RSMAGPIO
[switch to API](../../../Documentation/ScriptingAPI/en/GPIOPort.cs.md)

Implements properties and functionality of GPIO

## Constants
| Constant | Description | Type |
|--|--|--|
|High|Constant value of GPIO port pin in active state|float|
|Low|Constant value of GPIO port pin in inactive state|float|
## Methods
### void WritePin(string portName, string pinName, float value)
Remark:Works with pins in output mode only Toggles output value of GPIO port pin
Returns: 

#### Parameters
| Name | Description |
|--|--|
|value|GPIO port pin value|
|pinName|GPIO pin name|
|portName|GPIO port name|
### void WritePin(int portIndex, int pinIndex, float value)
Remark:Works with pins in output mode only Toggles output value of GPIO port pin
Returns: 

#### Parameters
| Name | Description |
|--|--|
|value|GPIO port pin value|
|pinIndex|GPIO pin index|
|portIndex|GPIO port index|
### void WritePin(int portIndex, string pinName, float value)
Remark:Works with pins in output mode only Toggles output value of GPIO port pin
Returns: 

#### Parameters
| Name | Description |
|--|--|
|value|GPIO port pin value|
|pinName|GPIO pin name|
|portIndex|GPIO port index|
### void WritePin(string portName, int pinIndex, float value)
Remark:Works with pins in output mode only Toggles output value of GPIO port pin
Returns: 

#### Parameters
| Name | Description |
|--|--|
|value|GPIO port pin value|
|pinIndex|GPIO pin index|
|portName|GPIO port name|
### GPIOPin GetPin(string portName, string pinName)
Gets GPIO port pin object by port name and pin name
Returns: 
Returns GPIOPin ref if it exists, else returns null
#### Parameters
| Name | Description |
|--|--|
|pinName|GPIO pin name|
|portName|GPIO port name|
### GPIOPin GetPin(ConnectedPin connectedPin)
Gets GPIO port pin object by ConnectedPin enum
Returns: 
Returns GPIOPin ref if it exists, else returns null
#### Parameters
| Name | Description |
|--|--|
|connectedPin|Connected GPIO port pin description|
### void ResetAll()
Sets default GPIO ports pins values
Returns: 

### void TurnOffAll()
Sets GPIO ports pins values to 0
Returns: 

### float ReadPin(string portName, string pinName)
Read input value of GPIO port pin
Returns: 
Returns float GPIO port pin value
#### Parameters
| Name | Description |
|--|--|
|pinName|GPIO pin name|
|portName|GPIO port name|
### float ReadPin(int portIndex, int pinIndex)
Read input value of GPIO port pin
Returns: 
Returns float GPIO port pin value
#### Parameters
| Name | Description |
|--|--|
|pinIndex|GPIO pin name|
|portIndex|GPIO port name|
### float ReadPin(int portIndex, string pinName)
Read input value of GPIO port pin
Returns: 
Returns float GPIO port pin value
#### Parameters
| Name | Description |
|--|--|
|pinName|GPIO pin name|
|portIndex|GPIO port name|
### float ReadPin(string portName, int pinIndex)
Read input value of GPIO port pin
Returns: 
Returns float GPIO port pin value
#### Parameters
| Name | Description |
|--|--|
|pinIndex|GPIO pin name|
|portName|GPIO port name|
