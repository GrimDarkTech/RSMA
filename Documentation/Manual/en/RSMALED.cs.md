# RSMALED
[switch to API](../../../Documentation/ScriptingAPI/en/RSMAKeyboard.cs.md)

Implements properties and functionality of Light Emitting Diode

## Fields
| Field | Description | Type |
|--|--|--|
|colorBody|LED body|Renderer|
|color||LED|
|color|LED color|Color|
|connectedPin|GPIO port pin which LED is connected|ConnectedPin|
|connectMicrocontroller|GPIO which LED is connected|RSMAGPIO|
## Methods
### void SetMode(ushort mode)
Sets LED mode
Returns: 

#### Parameters
| Name | Description |
|--|--|
|mode|LED mode (1 - active, else - inactive)|
