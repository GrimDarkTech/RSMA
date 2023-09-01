using UnityEngine;

/// <summary>
/// Implements behavior and properties of microcontroller programm
/// </summary>
public interface IMicrocontollerProgram
{
    /// <summary>
    /// Microcontroller GPIO
    /// </summary>
    public abstract RSMAGPIO GPIO { get; set; }

    /// <summary>
    /// Microcontroller databus for data transfer protocols
    /// </summary>
    public abstract RSMADataTransferMaster dataBus { get; set; }

    /// <summary>
    /// Entry point to microcontroller program
    /// </summary>
    public abstract System.Collections.IEnumerator MainProgramm();
    
}
