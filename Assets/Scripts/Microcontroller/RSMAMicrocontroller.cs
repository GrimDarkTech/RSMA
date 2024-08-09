using System.Collections;
using UnityEngine;

/// <summary>
/// Implements the properties and behaviors of AVR/STM-like microcontrollers. Contains GPIO, DataTransferMaster device and controller program
/// </summary>
public abstract class RSMAMicrocontroller : MonoBehaviour
{
    [SerializeField] protected RSMAGPIO GPIO;
    [SerializeField] protected RSMADataTransferMaster dataBus;
    [SerializeField] protected IMicrocontollerProgram program;
    protected IEnumerator programmCoroutine;

    protected virtual void OnEnable()
    {
        program = gameObject.GetComponent<IMicrocontollerProgram>();
        if (program != null)
        {
            program.GPIO = this.GPIO;
            program.dataBus = this.dataBus;
            programmCoroutine = program.MainProgramm();
            StartCoroutine(programmCoroutine);
        }
        else
        {
            Debug.LogError($"Microcontroller {gameObject.name} can't find programm file");
        }
    }
    protected virtual void OnDisable()
    {
        if (program != null)
        {
            StopCoroutine(programmCoroutine);
        }
        GPIO.TurnOffAll();
    }
}
