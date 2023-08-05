using System.Collections;
using UnityEngine;

public class RSMAMicrocontroller : MonoBehaviour
{
    [SerializeField] protected RSMAGPIO GPIO;
    [SerializeField] protected RSMADataTransferMaster dataBus;
    protected void Start()
    {
        StartCoroutine(MicroLoop());
    }

    protected virtual IEnumerator MicroLoop()
    {
        yield return new WaitForSeconds(.1f);
    }
}
