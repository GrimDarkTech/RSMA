using UnityEngine.UI;
using UnityEngine;

/// <summary>
/// Implements properties and functionality of text display
/// </summary>
public class RSMADisplay : RSMADataTransferSlave
{
    /// <summary>
    /// TextMesh component used to display text
    /// </summary>
    public TextMesh text;

    public override void ReciveData(string recivedData)
    {
        text.text = recivedData;
    }
}
