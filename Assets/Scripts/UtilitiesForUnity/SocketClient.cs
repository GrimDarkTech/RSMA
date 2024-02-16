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
    public string message = "";


    [SerializeField]
    private bool isConnected;


    private Socket client;

    [ContextMenu("Connect to server")]
    private async void ConnectToServerAsync()
    {
        if (isConnected)
        {
            SocketLogger.Log($"{clientName}: Ñlient is already connected to server");
            return;
        }
        IPAddress ipAddress = IPAddress.Parse(serverIP);

        IPEndPoint ipEndPoint = new(ipAddress, serverPort);

        client = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        await client.ConnectAsync(ipEndPoint);

        SocketLogger.Log($"{clientName}: Waiting for server responce");

        while (true)
        {
            string message = $"{clientName}<|CCR|><|EOM|>";
            var messageBytes = Encoding.UTF8.GetBytes(message);

            _ = await client.SendAsync(messageBytes, SocketFlags.None);

            var buffer = new byte[1024];
            var received = await client.ReceiveAsync(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);
            if (response == $"{clientName}<|SA|><|ACK|>")
            {
                SocketLogger.Log($"{clientName}: successfully connect to server.");
                isConnected = true;
                break;
            }
        }
    }
    [ContextMenu("Send message to server")]
    public void SendMes()
    {
        SendMessageToServerAsync(message);
    }
    private async void SendMessageToServerAsync(string message)
    {
        if (!isConnected)
        {
            SocketLogger.Log($"{clientName}: client is NOT connected to server");
            return;
        }
        message = $"{clientName}<|CM|>" + message + "<|EOM|>";
        var messageBytes = Encoding.UTF8.GetBytes(message);

        _ = await client.SendAsync(messageBytes, SocketFlags.None);

        var buffer = new byte[1024];
        var received = await client.ReceiveAsync(buffer, SocketFlags.None);
        var response = Encoding.UTF8.GetString(buffer, 0, received);
        if (response == $"{clientName}<|SA|><|ACK|>")
        {
            SocketLogger.Log($"{clientName}: Message delivered");
        }
    }
    [ContextMenu("Disconnet from server")]
    private async void ShutdownClientAsync()
    {
        if (!isConnected)
        {
            SocketLogger.Log($"{clientName}: Ñlient is NOT connected to server");
            return;
        }
        string message = $"{clientName}<|CDR|><|EOM|>";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _ = await client.SendAsync(messageBytes, SocketFlags.None);

        var buffer = new byte[1024];
        var received = await client.ReceiveAsync(buffer, SocketFlags.None);
        var response = Encoding.UTF8.GetString(buffer, 0, received);
        if (response == $"{clientName}<|SA|><|ACK|>")
        {
            SocketLogger.Log($"{clientName}: Shutdown request has been accepted");
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
            isConnected = false;
        }
        SocketLogger.Log($"{clientName}: Disconnected");
    }
}

// massage struct: target/clientName<separator>messageText<|EOM|>

// separators: <|EOM|> - end of message;
// <|M|> - message separator
// <|CR|> - client connection request separator
// <|DR|> - client disconnection request separator'
// <|ACK|> - ack separator

// ack struct: target/clientName<|ACK|>