using UnityEngine.UI;
using UnityEngine;

/// <summary>
/// Implements properties and functionality of text display
/// </summary>
public class RSMADisplay : RSMADataTransferSlave
{
    [SerializeField] private TextMesh text;

    public override void ReciveData(string recivedData)
    {
        text.text = recivedData;
    }
}
