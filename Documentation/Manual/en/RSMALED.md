# RSMALED
[switch to API](../../../Documentation/ScriptingAPI/en/RSMALED.md)

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
| Declaration | Description | Returns |
|--|--|--|
|void SetMode(ushort mode)|Sets LED mode||
### Parameters
| Name | Description |
|--|--|
|mode|LED mode (1 - active, else - inactive)|
