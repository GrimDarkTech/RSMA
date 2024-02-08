using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using System;

public class SocketClient : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    /// <summary>
    /// Server IP address
    /// </summary>
    public int serverPort = 7777;
    /// <summary>
    /// Port binded by the server
    /// </summary>
    public string clientName = "";
    /// <summary>
    /// Name of client
    /// </summary>

    public void Connect()
    {
        ConnectToServer();
    }

    private async void ConnectToServer()
    {
        IPAddress ipAddress = IPAddress.Parse(serverIP);

        IPEndPoint ipEndPoint = new(ipAddress, serverPort);
        Socket client = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        await client.ConnectAsync(ipEndPoint);

        Debug.Log("Waiting for server responce");

        while (true)
        {
            string message = $"{clientName}<|EOM|>";
            var messageBytes = Encoding.UTF8.GetBytes(message);

            _ = await client.SendAsync(messageBytes, SocketFlags.None);

            var buffer = new byte[1024];
            var received = await client.ReceiveAsync(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);
            if (response == "<|ACK|>")
            {
                Debug.Log($"Client successfully connect to server.");
                break;
            }
        }

        try
        {
            client.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            client.Close();
        }
    }
}
