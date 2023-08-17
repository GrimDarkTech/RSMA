using System;

[Serializable]
public class GPIOPin
{
    public string name;
    public float value;
    public PinModes pinMode;
    public PinPullModes pinPullMode;

    public float outputDefaulValue;
    public PinOutputModes pinOutputMode;
}
public enum PinModes
{
    Output,
    Input
}
public enum PinPullModes
{
    NoPullUpDown,
    PullUp,
    PullDown
}

public enum PinOutputModes
{
    PushPull,
    OpenDrain
}