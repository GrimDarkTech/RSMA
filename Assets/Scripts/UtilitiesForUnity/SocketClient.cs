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

    private Socket client;

    [ContextMenu("Connect to server")]
    private async void ConnectToServerAsync()
    {
        IPAddress ipAddress = IPAddress.Parse(serverIP);

        IPEndPoint ipEndPoint = new(ipAddress, serverPort);

        client = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

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
            if (response == $"{clientName}<|ACK|>")
            {
                Debug.Log($"Client successfully connect to server.");
                break;
            }
        }
    }

    private async void SendMessageToServerAsync(string message)
    {
        message = $"{client}<CNS>" + message + "<|EOM|>";
        var messageBytes = Encoding.UTF8.GetBytes(message);

        _ = await client.SendAsync(messageBytes, SocketFlags.None);

        var buffer = new byte[1024];
        var received = await client.ReceiveAsync(buffer, SocketFlags.None);
        var response = Encoding.UTF8.GetString(buffer, 0, received);
        if (response == $"<SS>{clientName}<|ACK|>")
        {
            Debug.Log($"Message delivered");
        }
    }
    private void ShutdownClient()
    {
        string message = $"{client}<CNS>" +  + "<|EOM|>";
        _ = await client.SendAsync(messageBytes, SocketFlags.None);

        var buffer = new byte[1024];
        var received = await client.ReceiveAsync(buffer, SocketFlags.None);
        var response = Encoding.UTF8.GetString(buffer, 0, received);
        if (response == $"<SS>{clientName}<|ACK|>")
        {
            Debug.Log($"Message delivered");
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

// massage struct: target/clientName<separator>messageText<|EOM|>

// separators: <|EOM|> - end of message;
// <|CM|> - client message separator
// <|SM|> - server message separator
// <|CCR|> - client connection request separator
// <|CDR|> - client disconnection request separator'
// <|CA|> - client ack separator
// <|SA|> - server ack separator

// ack struct: target/clientName<separator><|ACK|>