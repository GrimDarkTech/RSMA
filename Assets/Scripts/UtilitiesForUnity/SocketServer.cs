using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using System;
using UnityEditor.VersionControl;

public class SocketServer : MonoBehaviour 
{
    public string serverIP = "127.0.0.1";
    /// <summary>
    /// Server IP address
    /// </summary>
    public int serverPort = 7777;
    /// <summary>
    /// Port binded by the server
    /// </summary>
    public List<string> clients = new List<string>();
    public void Start()
    {
        StartServer();
    }

    private async void StartServer()
    {

        IPAddress ipAddress = IPAddress.Parse(serverIP);

        IPEndPoint ipEndPoint = new(ipAddress, serverPort);

        Socket listener = new(ipEndPoint.AddressFamily,SocketType.Stream,ProtocolType.Tcp);

        try
        {
            listener.Bind(ipEndPoint);
        }
        catch(Exception ex)
        {
            Debug.LogException(ex);
        }
        finally 
        {
            Debug.Log("Server successfully binded the port");
        }
        
        listener.Listen(100);

        var handler = await listener.AcceptAsync();

        while (true)
        {
            byte[] buffer = new byte[1024];
            int received = await handler.ReceiveAsync(buffer, SocketFlags.None);
            string response = Encoding.UTF8.GetString(buffer, 0, received);

            string endOfMessage = "<|EOM|>";

            if (response.IndexOf(endOfMessage) > -1)
            {
                Debug.Log($"Connecting attempt from \"{response.Replace(endOfMessage, "")}\"");

                string ackMessage = "<|ACK|>";
                byte[] echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                await handler.SendAsync(echoBytes, 0);

                clients.Add(response.Replace(endOfMessage, ""));

                break;
            }
        }
    }
}
