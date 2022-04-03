using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MicrocontrollerBaseScript : MonoBehaviour
{
    public bool isActive;
    protected bool isLoop = false;
    private bool isLoopEnd = false;
    [SerializeField] private GPIOBaseScript GPIO;
    private void Start()
    {
        if(isActive)
        MicroStart();
    }
    private void Update()
    {
        if(!isLoopEnd && isLoop)
            StartCoroutine(MicroLoop());
    }
    private void MicroStart()
    {
        StartCoroutine(MicroLoop());
        isLoop = true;
    }
    private IEnumerator MicroLoop()
    {
        yield return new WaitForSeconds(.1f);
        isLoopEnd = false;
    }

}
