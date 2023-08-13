using UnityEngine;

public interface IMicrocontollerProgramm
{
    public abstract RSMAGPIO GPIO { get; set; }
    public abstract RSMADataTransferMaster dataBus { get; set; }

    public abstract System.Collections.IEnumerator MicroLoop();
    
}
