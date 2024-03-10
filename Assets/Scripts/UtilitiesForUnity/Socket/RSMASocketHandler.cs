using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSMASocketHandler: MonoBehaviour
{
    public static RSMAServer server;

    public static void Start(string serverIP, int serverPort)
    {
        server = new RSMAServer();

        server.serverIP = serverIP;
        server.serverPort = serverPort;

        server.Run();
    }
}

public class RSMAServer : SocketServer
{
    public override void OnClientConnected(string clientName)
    {
        base.OnClientConnected(clientName);
    }
}

