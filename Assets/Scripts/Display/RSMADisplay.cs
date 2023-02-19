using UnityEngine.UI;
using UnityEngine;

public class RSMADisplay : RSMADataTransferSlave
{
    [SerializeField] private TextMesh text;

    public override void ReciveData(string recivedData)
    {
        text.text = recivedData;
    }
}
