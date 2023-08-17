using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GPIOPort
{
    public string name;
    [SerializeField] public List<GPIOPin> pins = new List<GPIOPin>();
}
