using System;

/// <summary>
/// Describes GPIO port pin which device is connected
/// </summary>
[Serializable]
public struct ConnectedPin
{
    public string portName;
    public string pinName;
}
