# Switch
[switch to API](../../../Documentation/ScriptingAPI/en/RSMALimitSwitch.cs.md)



## Fields
| Field | Description | Type |
|--|--|--|
|TypeEnum||SwitchTypeEnum|
|MechState|The state of closure or opening of an electrical circuit.|bool|
|ButtonDamper|Resistance to movement due to viscous friction.|float|
|ButtonSpring|The force returns to its previous shape after compression or stretching.|float|
|MaxLength|Maximum button pressing distance|float|
|HorButtonSlider|of the button, which is displayed as a percentage.the activation point is within the initial position to the limit position|float|
|AngularDamper|Resistance to movement due to viscous friction for angle.|float|
|AngularSpring|the angular view.The force returns to its previous shape after compression or stretching in|float|
|MinAngle|The minimum angle at which the lever can turn.|float|
|MaxAngle|The maximum angle at which the lever can turn|float|
|HorLeverRollerSlider|of the lever, which is displayed as a percentage.the activation point is within the initial angle to the limit angle|float|
|Joint|The joint is a lever responsible for simulating the behavior of the lever.|ConfigurableJoint|
|AnchorPos|Fulcrum of configurable joint.|Vector3|
|Vector|Direction of the axis of rotation.|Vector3|
|SizeGizmoSphere|The size of the gizmo that is displayed in the unity user interface.|float|
|DoCalculations|An event that performs calculations depending on the type of limit switch.|Action|
## Methods
### void SwitchOn()
It is triggered when the limit switch switches to the on state
Returns: 

### void SwitchOff()
It is triggered when the limit switch switches to the off state
Returns: 

### void ResetJoint()
Erases all settings of configurable joint of lever and sets new ones
Returns: 

# SwitchEditor
[switch to API](../../../Documentation/ScriptingAPI/en/RSMALimitSwitch.cs.md)

Deleted in the final build.Settings for the unique display of limit switch data.

