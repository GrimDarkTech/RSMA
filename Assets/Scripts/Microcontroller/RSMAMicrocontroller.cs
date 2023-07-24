using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSMAMicrocontroller : MonoBehaviour
{
    public bool isActive;
    protected bool isLoop = false;
    protected bool isLoopEnd = false;
    [SerializeField] protected RSMAGPIO GPIO;
    [SerializeField] protected RSMADataTransferMaster dataBus;
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
        //TODO убрать
        Debug.Log("TractorMicro B");

        yield return new WaitForSeconds(.1f);
        isLoopEnd = false;
    }

}
