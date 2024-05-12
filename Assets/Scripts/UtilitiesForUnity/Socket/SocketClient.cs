using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using System;

/// <summary>
/// Ñlass implements the behavior of a socket client. Uses to connect to server, send and receive messages
/// </summary>
public class SocketClient
{
    /// <summary>
    /// Server IP address
    /// </summary>
    public string serverIP = "127.0.0.1";

    /// <summary>
    /// Port binded by the server
    /// </summary>
    public int serverPort = 7777;

    /// <summary>
    /// Name of client
    /// </summary>
    public string clientName = "";

    /// <summary>
    /// True if client connected to server
    /// </summary>
    public bool isConnected = false;

    /// <summary>
    /// Uses to contain task for listening for messages
    /// </summary>
    protected Task messageReciver;

    /// <summary>
    /// Socket client
    /// </summary>
    protected Socket client;

    public SocketClient() { }
    public SocketClient(string serverIP, int serverPort, string clientName)
    {
        this.serverIP = serverIP;
        this.serverPort = serverPort;
        this.clientName = clientName;
    }

    /// <summary>
    /// Called when client connected to server
    /// </summary>
    public virtual void OnConnected() { }

    /// <summary>
    /// Called when client disconnected to server
    /// </summary>
    public virtual void OnDisconnected() { }

    /// <summary>
    /// Called when message delivered to server
    /// </summary>
    /// <param name="message">Message text</param>
    public virtual void OnMessageDelivered() { }

    /// <summary>
    /// Called when recived message from server
    /// </summary>
    /// <param name="message">Message text</param>
    public virtual void OnMessageRecived(string message) { }

    /// <summary>
    /// Connects client to server
    /// </summary>
    public async void ConnectAsync()
    {
        if (isConnected)
        {
            RSMALogger.Logger.Log($"{clientName}: Ñlient is already connected to server");
            return;
        }
        IPAddress ipAddress = IPAddress.Parse(serverIP);

        IPEndPoint ipEndPoint = new(ipAddress, serverPort);

        client = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try 
        {
            await client.ConnectAsync(ipEndPoint);
        }
        catch(Exception ex)
        {
            RSMALogger.Logger.Log($"{clientName}: Connection error: The destination computer rejected the connection request. Make sure that the server is running and available for connection");
            return;
        }

        RSMALogger.Logger.Log($"{clientName}: Waiting for server responce");

        while (true)
        {
            string message = $"{clientName}<|CR|><|EOM|>";
            var messageBytes = Encoding.UTF8.GetBytes(message);
            try
            {
                _ = await client.SendAsync(messageBytes, SocketFlags.None);
            }
            catch (Exception ex)
            {
                RSMALogger.Logger.Log($"{clientName}: Connection error: The destination computer rejected the connection request. Make sure that the server is running and available for connection");
                return;
            }
            var buffer = new byte[1024];
            int received = 0;
            try
            {
                received = await client.ReceiveAsync(buffer, SocketFlags.None);
            }
            catch (Exception ex)
            {
                RSMALogger.Logger.Log($"{clientName}: Connection error: The destination computer rejected the connection request. Try change client/user name");
                return;
            }
            var response = Encoding.UTF8.GetString(buffer, 0, received);

            if (response == $"<|CR|><|ACK|>")
            {
                RSMALogger.Logger.Log($"{clientName}: successfully connect to server.");
                isConnected = true;
                break;
            }
        }

        messageReciver = Task.Run(() => ListenToMessageAsync());

        OnConnected();
    }
    /// <summary>
    /// Sends message to server
    /// </summary>
    /// <param name="message">Message text</param>
    public async void SendMessageAsync(string message)
    {
        if (!isConnected)
        {
            RSMALogger.Logger.Log($"{clientName}: client is NOT connected to server");
            return;
        }
        message = $"{clientName}<|M|>" + message + "<|EOM|>";
        var messageBytes = Encoding.UTF8.GetBytes(message);

        try
        {
            _ = await client.SendAsync(messageBytes, SocketFlags.None);
        }
        catch(Exception ex)
        {
            RSMALogger.Logger.Log($"{clientName}: Listener error: The remote computer has terminated an existing connection. Make sure that the server is running and available for connection");
            isConnected = false;
            return;
        } 
    }
    /// <summary>
    /// Disconnects cleint from server
    /// </summary>
    public async void DisconnectAsync()
    {
        if (!isConnected)
        {
            RSMALogger.Logger.Log($"{clientName}: Ñlient is NOT connected to server");
            return;
        }
        string message = $"{clientName}<|DR|><|EOM|>";
        var messageBytes = Encoding.UTF8.GetBytes(message);

        try
        {
            _ = await client.SendAsync(messageBytes, SocketFlags.None);
        }
        catch (Exception ex)
        {
            RSMALogger.Logger.Log($"{clientName}: Listener error: The remote computer has terminated an existing connection. Make sure that the server is running and available for connection");
            isConnected = false;
            return;
        }
    }
    /// <summary>
    /// Listening to messages
    /// </summary>
    protected async void ListenToMessageAsync()
    {
        while (true)
        {
            byte[] buffer = new byte[1024];
            int receivedBites = 0;
            try
            {
                receivedBites = await client.ReceiveAsync(buffer, SocketFlags.None);
            }
            catch
            {
                RSMALogger.Logger.Log($"{clientName}: Listener error: The remote computer has terminated an existing connection. Make sure that the server is running and available for connection");
                isConnected = false;
                return;
            }
            string receivedText = Encoding.UTF8.GetString(buffer, 0, receivedBites);

            if (receivedText.IndexOf("<|EOM|>") > -1 || receivedText.IndexOf("<|ACK|>") > -1)
            {
                string[] messages = receivedText.Split("<|EOM|>");
                string[] acks = receivedText.Split("<|ACK|>");

                if(messages.Length > 1)
                {
                    foreach (var message in messages)
                    {
                        if (message.IndexOf("<|M|>") > -1)
                        {
                            string text = message.Split("<|M|>")[1];

                            string ackMessage = $"<|M|><|ACK|>";
                            byte[] echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                            await client.SendAsync(echoBytes, 0);

                            RSMALogger.Logger.Log($"{clientName}: recived message from server");

                            OnMessageRecived(text);
                        }
                    }
                }

                if (acks.Length > 1)
                {
                    foreach (var ack in acks)
                    {
                        if (ack == "<|M|>")
                        {
                            RSMALogger.Logger.Log($"{clientName}: Message delivered");
                            OnMessageDelivered();
                        }
                        else if (ack == "<|DR|>")
                        {
                            try
                            {
                                messageReciver.Dispose();
                                client.Shutdown(SocketShutdown.Both);
                            }
                            catch (Exception ex)
                            {
                                RSMALogger.Logger.Log(ex.Message);
                                return;
                            }
                            finally
                            {
                                client.Close();
                                isConnected = false;
                                RSMALogger.Logger.Log($"{clientName}: Disconnected");
                                OnDisconnected();
                            }
                            return;
                        }
                    }
                }
            }
        }
    }
}

// massage struct: target/clientName<separator>messageText<|EOM|>

// separators: <|EOM|> - end of message;
// <|M|> - message separator
// <|CR|> - client connection request separator
// <|DR|> - client disconnection request separator'
// <|ACK|> - ack separator

// ack struct: <separator><|ACK|>