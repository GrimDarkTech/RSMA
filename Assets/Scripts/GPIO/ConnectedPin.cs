using System;

/// <summary>
/// Describes GPIO port pin which device is connected
/// </summary>
[Serializable]
public struct ConnectedPin
{
    /// <summary>
    /// GPIO port name
    /// </summary>
    public string portName;
    /// <summary>
    /// GPIO port pin name
    /// </summary>
    public string pinName;
}
