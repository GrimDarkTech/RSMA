using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MicrocontrollerBase : MonoBehaviour
{
    public bool isActive;
    protected bool isLoop = false;
    protected bool isLoopEnd = false;
    [SerializeField] protected GPIOBase GPIO;
    [SerializeField] protected DataTransferMasterScript dataBus;
    protected void Start()
    {
        if(isActive)
            StartCoroutine(MicroStart()); ;
    }
    protected void Update()
    {
        if(!isLoopEnd && isLoop)
            StartCoroutine(MicroLoop());
    }
    protected IEnumerator MicroStart()
    {
        yield return new WaitForSeconds(.1f);
        StartCoroutine(MicroLoop());
        isLoop = true;
    }
    protected IEnumerator MicroLoop()
    {
        yield return new WaitForSeconds(.1f);
        isLoopEnd = false;
    }

}
