using UnityEngine.UI;
using UnityEngine;

public class DisplayDataTransfer : DataTransgerSlaveScript
{
    [SerializeField] private TextMesh text;

    public override void ReciveData(string recivedData)
    {
        text.text = recivedData;
    }
}
