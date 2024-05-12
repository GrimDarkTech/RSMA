using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using System;

/// <summary>
/// Сlass implements the behavior of a socket server. Uses to connect clients, send and receive messages
/// </summary>
public class SocketServer
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
    /// Clients names
    /// </summary>
    public List<string> cliensNames = new List<string>();

    /// <summary>
    /// Clients connected to server
    /// </summary>
    protected Dictionary<string, Socket> clients = new Dictionary<string, Socket>();

    protected Dictionary<string, Task> messageRecivers = new Dictionary<string, Task>();

    /// <summary>
    /// True if server started
    /// </summary>
    public bool isStarted = false;

    Socket listener;

    public SocketServer() { }
    public SocketServer(string serverIP, int serverPort)
    {
        this.serverPort = serverPort;
    }

    /// <summary>
    /// Called on server ready to listen clients
    /// </summary>
    public virtual void OnStarted() { }

    /// <summary>
    /// Called on server stopped
    /// </summary>
    public virtual void OnStopped() { }

    /// <summary>
    /// Called when new client connected
    /// </summary>
    public virtual void OnClientConnected(string clientName) { }

    /// <summary>
    /// Called when client disconnected
    /// </summary>
    /// <param name="clientName">Name of disconnected client</param>
    public virtual void OnClientDisconnected(string clientName) { }

    /// <summary>
    /// Called when message delivered to client
    /// </summary>
    /// <param name="clientName">Target client</param>
    /// <param name="message">Message text</param>
    public virtual void OnMessageDelivered(string clientName) { }

    /// <summary>
    /// Called when message recived
    /// </summary>
    /// <param name="clientName">Client sended message</param>
    /// <param name="message">Message text</param>
    public virtual void OnMessageRecived(string clientName, string message) { }

    /// <summary>
    /// Starts server
    /// </summary>
    public void Run()
    {
        if (isStarted)
        {
            RSMALogger.Logger.Log("Server: Server already started");
            return;
        }

        IPEndPoint ipEndPoint = new(IPAddress.Any, serverPort);

        listener = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            listener.Bind(ipEndPoint);
        }
        catch(Exception ex)
        {
            RSMALogger.Logger.Log(ex.Message);
            return;
        }
        finally 
        {
            RSMALogger.Logger.Log("Server: Successfully binded the port");
        }
        
        listener.Listen(100);

        clients.Clear();

        Task listenClient = Task.Run(() => ListenToClientAsync(listener, clients));

        RSMALogger.Logger.Log("Server: Listener is enabled");

        isStarted = true;
        OnStarted();
    }
    /// <summary>
    /// Stops server
    /// </summary>
    public void Stop()
    {
        if (isStarted)
        {
            RSMALogger.Logger.Log("Server: Server NOT started");
            return;
        }
        try
        {
            listener.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex)
        {
            RSMALogger.Logger.Log(ex.Message);
        }
        finally
        {
            listener.Close();
            isStarted = false;
            RSMALogger.Logger.Log($"Server: Server closed");
            OnStopped();
        }
    }
    /// <summary>
    /// Uses to connect new clients
    /// </summary>
    /// <param name="listener">Server listener reference</param>
    /// <param name="clients">Contains clients sockets. Uses client name as key</param>
    protected async void ListenToClientAsync(Socket listener, Dictionary<string, Socket> clients)
    {
        while (true)
        {
            Socket handler = await listener.AcceptAsync();

            while (true)
            {
                byte[] buffer = new byte[1024];
                int received = await handler.ReceiveAsync(buffer, SocketFlags.None);
                string response = Encoding.UTF8.GetString(buffer, 0, received);


                if (response.IndexOf("<|EOM|>") > -1 && response.IndexOf("<|CR|>") > -1)
                {
                    string clientName = response.Replace("<|EOM|>", "");
                    clientName = clientName.Replace("<|CR|>", "");

                    RSMALogger.Logger.Log($"Server: Connecting attempt from \"{clientName}\"");

                    if (cliensNames.Contains(clientName))
                    {
                        RSMALogger.Logger.Log($"Server: client with name \"{clientName}\" is already connected!");
                        handler.Close();
                        return;
                    }

                    string ackMessage = "<|CR|><|ACK|>";
                    byte[] echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                    await handler.SendAsync(echoBytes, 0);

                    clients.Add(clientName, handler);
                    cliensNames.Add(clientName);

                    messageRecivers.Add(clientName, Task.Run(() => ListenToMessageAsync(handler, clientName)));

                    OnClientConnected(clientName);
                    break;
                }
            }
        }
    }
    /// <summary>
    /// Listens to messages
    /// </summary>
    /// <param name="client">Socket client</param>
    /// <param name="clientName">Name of client</param>
    protected async void ListenToMessageAsync(Socket client, string clientName)
    {
        while(true)
        {
            byte[] buffer = new byte[1024];
            int receivedBites = 0;
            try
            {
                receivedBites = await client.ReceiveAsync(buffer, SocketFlags.None);
            }
            catch(Exception ex)
            {
                RSMALogger.Logger.Log($"Server: Connection error: The remote client has terminated an existing connection.");

                client.Shutdown(SocketShutdown.Both);
                client.Close();
                clients.Remove(clientName);
                cliensNames.Remove(clientName);
                messageRecivers[clientName].Dispose();
                messageRecivers.Remove(clientName);

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
                            var separetedValues = message.Split("<|M|>");
                            string text = separetedValues[1];

                            string ackMessage = $"<|M|><|ACK|>";
                            byte[] echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                            await client.SendAsync(echoBytes, 0);

                            RSMALogger.Logger.Log($"Server: Recived message from {clientName}");

                            OnMessageRecived(clientName, text);
                        }

                        else if (message.IndexOf("<|DR|>") > -1)
                        {
                            string ackMessage = "<|DR|><|ACK|>";
                            byte[] echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                            await client.SendAsync(echoBytes, 0);

                            clients.Remove(clientName);
                            cliensNames.Remove(clientName);
                            messageRecivers[clientName].Dispose();
                            messageRecivers.Remove(clientName);

                            RSMALogger.Logger.Log($"Server: Client {clientName} disconnected");
                            OnClientDisconnected(clientName);

                            return;
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
                            OnMessageDelivered(clientName);
                        }
                    }
                }
            }
        }
    }
    /// <summary>
    /// Sends message to client
    /// </summary>
    /// <param name="clientName">Target client</param>
    /// <param name="message">Message text</param>
    public async void SendMessageToClientAsync(string clientName, string message)
    {
        Socket client;
        try
        {
            client = clients[clientName];
        }
        catch(Exception e)
        {
            RSMALogger.Logger.Log($"Server: Client {clientName} is NOT connected");
            return;
        }

        if (!client.Connected)
        {
            clients.Remove(clientName);
            cliensNames.Remove(clientName);

            RSMALogger.Logger.Log($"Server: Client {clientName} disconnected");
            return;
        }
        message = $"{clientName}<|M|>" + message + "<|EOM|>";

        var messageBytes = Encoding.UTF8.GetBytes(message);

        _ = await client.SendAsync(messageBytes, SocketFlags.None);
    }
}
