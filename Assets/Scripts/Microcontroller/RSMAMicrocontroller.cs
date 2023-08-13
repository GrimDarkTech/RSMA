using System.Collections;
using UnityEngine;

public abstract class RSMAMicrocontroller : MonoBehaviour
{
    [SerializeField] protected RSMAGPIO GPIO;
    [SerializeField] protected RSMADataTransferMaster dataBus;
    [SerializeField] protected IMicrocontollerProgramm programm;

    protected void OnEnable()
    {
        StartCoroutine(programm.MicroLoop());
        if(programm != null)
        {
            programm.GPIO = this.GPIO;
            programm.dataBus = this.dataBus;

        }
    }
}
