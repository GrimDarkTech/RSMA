using System.Collections;
using UnityEngine;

public abstract class RSMAMicrocontroller : MonoBehaviour
{
    [SerializeField] protected RSMAGPIO GPIO;
    [SerializeField] protected RSMADataTransferMaster dataBus;
    [SerializeField] protected IMicrocontollerProgramm programm;

    protected void OnEnable()
    {
        programm = gameObject.GetComponent<IMicrocontollerProgramm>();
        if (programm != null)
        {
            programm.GPIO = this.GPIO;
            programm.dataBus = this.dataBus;
            StartCoroutine(programm.MainProgramm());
        }
        else
        {
            Debug.LogError($"Microcontroller {gameObject.name} can't find programm file");
        }
    }
}
