using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test class for autocds
/// </summary>
public class tota : MonoBehaviour
{
    /// <summary>
    /// Test fielld
    /// </summary>
    public string tField;

    /// <summary>
    /// Test propertie
    /// </summary>
    public string tValue { get; set; }

    /// <summary>
    /// Test method
    /// </summary>
    /// <param name="aboba">test aboba param</param>
    /// <returns>Returns tField values</returns>
    public string tMethod(string aboba)
    {
        return tField;
    }

    /// <summary>
    /// Test method n2
    /// </summary>
    /// <param name="aboba2">test aboba param</param>
    public void t2Method(string aboba2)
    {

    }

    /// <summary>
    /// Test method 3
    /// </summary>
    public void t3Method()
    {

    }

    /// <summary>
    /// Test method 4
    /// </summary>
    /// <returns>ASA stringa</returns>
    public string t4Method()
    {
        return "as";
    }
}
