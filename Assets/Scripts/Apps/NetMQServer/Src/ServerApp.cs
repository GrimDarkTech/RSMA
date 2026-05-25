using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerApp : MonoBehaviour
{
    //private RSMANetMQServer server = null;
    void Start()
    {
        //server = new RSMANetMQServer();
        Debug.Log("Server: Start");
    }
    [ContextMenu("Run Server")]
    public void Run() 
    {
        //server.Start();
        Debug.Log("Server: Run");
    }

    [ContextMenu("Stop Server")]
    public void Stop()
    {
        //server.Stop();
        Debug.Log("Server: Stop");
    }
}
