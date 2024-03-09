using System.Threading;
using UnityEngine;

public class RSMAServer
{
    public static RSMASocket server;

    public static void Start(string serverIP, int serverPort)
    {
        server = new RSMASocket();

        server.serverIP = serverIP;
        server.serverPort = serverPort;

        server.Run();
    }
}
public class RSMASocket : SocketServer
{
    public override void OnClientConnected(string clientName)
    {
        Debug.Log($"Client : {clientName} is connected");
    }

    public override void OnMessageRecived(string clientName, string message)
    {
        Debug.Log($"Message from : {clientName}: {message}");
    }
}