using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements properties and functionality of GPIO port
/// </summary>
[Serializable]
public class GPIOPort
{
    /// <summary>
    /// GPIO port name
    /// </summary>
    public string name;

    /// <summary>
    /// List of GPIO port pins
    /// </summary>
    [SerializeField] public List<GPIOPin> pins = new List<GPIOPin>();
}
