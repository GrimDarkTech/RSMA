using UnityEngine.UI;
using UnityEngine;

public class DisplayI2C : I2CSlaveScript
{
    [SerializeField] private TextMesh text;

    public override void reciveData(string recivedData)
    {
        text.text = recivedData;
    }
}
