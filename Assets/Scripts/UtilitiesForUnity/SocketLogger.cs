using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SocketLogger : MonoBehaviour
{
    static string log;

    [TextAreaAttribute(10, 35)]
    public string logText = "";

    public static void Log(string text)
    {
        log += text + "\n";
    }

    private void Update()
    {
        logText = log;
    }
}
