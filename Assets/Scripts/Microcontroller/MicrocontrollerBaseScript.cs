using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MicrocontrollerBaseScript : MonoBehaviour
{
    public bool isActive;
    protected bool isLoop = false;
    protected bool isLoopEnd = false;
    [SerializeField] protected GPIOBaseScript GPIO;
    [SerializeField] protected I2CMasterScript I2CBus;
    protected void Start()
    {
        if(isActive)
        MicroStart();
    }
    protected void Update()
    {
        if(!isLoopEnd && isLoop)
            StartCoroutine(MicroLoop());
    }
    protected void MicroStart()
    {
        StartCoroutine(MicroLoop());
        isLoop = true;
    }
    protected IEnumerator MicroLoop()
    {
        yield return new WaitForSeconds(.1f);
        isLoopEnd = false;
    }

}
