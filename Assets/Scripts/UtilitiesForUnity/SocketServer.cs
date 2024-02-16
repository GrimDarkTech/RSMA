using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using System;
using System.Runtime.InteropServices;
using System.Runtime.ConstrainedExecution;
using static UnityEngine.EventSystems.EventTrigger;

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
    public List<string> cliensNames = new List<string>();
    /// <summary>
    /// Clients names
    /// </summary>

    private Dictionary<string, Socket> clients = new Dictionary<string, Socket>();
    /// <summary>
    /// Clients connected to server
    /// </summary>

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
            SocketLogger.Log(ex.Message);
        }
        finally 
        {
            SocketLogger.Log("Server: Successfully binded the port");
        }
        

        listener.Listen(100);

        clients.Clear();

        Task listenClient = Task.Run(() => ListenToClientAsync(listener, clients));

        SocketLogger.Log("Server: Listener is enabled");
    }
    private async void ListenToClientAsync(Socket listener, Dictionary<string, Socket> clients)
    {
        while (true)
        {
            Socket handler = await listener.AcceptAsync();

            while (true)
            {
                byte[] buffer = new byte[1024];
                int received = await handler.ReceiveAsync(buffer, SocketFlags.None);
                string response = Encoding.UTF8.GetString(buffer, 0, received);


                if (response.IndexOf("<|EOM|>") > -1 && response.IndexOf("<|CCR|>") > -1)
                {
                    string clientName = response.Replace("<|EOM|>", "");
                    clientName = clientName.Replace("<|CCR|>", "");

                    SocketLogger.Log($"Server: Connecting attempt from \"{clientName}\"");

                    string ackMessage = $"{clientName}<|SA|><|ACK|>";
                    byte[] echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                    await handler.SendAsync(echoBytes, 0);

                    clients.Add(clientName, handler);
                    cliensNames.Add(clientName);
                    Task ReciveMessage = Task.Run(() => ListenToMessageAsync(handler));

                    break;
                }
            }
        }
    }

    private async void ListenToMessageAsync(Socket client)
    {
        while(true)
        {
            byte[] buffer = new byte[1024];
            int received = await client.ReceiveAsync(buffer, SocketFlags.None);
            string response = Encoding.UTF8.GetString(buffer, 0, received);

            if (response.IndexOf("<|EOM|>") > -1)
            {
                response = response.Replace("<|EOM|>", "");

                string messageText;
                string clientName;
                if(response.IndexOf("<|CM|>") > -1)
                {
                    var separetedValues = response.Split("<|CM|>");
                    clientName = separetedValues[0];
                    messageText = separetedValues[1];

                    string ackMessage = $"{clientName}<|SA|><|ACK|>";
                    byte[] echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                    await client.SendAsync(echoBytes, 0);

                    SocketLogger.Log($"Recived message from {clientName}");
                }
                else if (response.IndexOf("<|CDR|>") > -1)
                {
                    var separetedValues = response.Split("<|CDR|>");
                    clientName = separetedValues[0];

                    string ackMessage = $"{clientName}<|SA|><|ACK|>";
                    byte[] echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                    await client.SendAsync(echoBytes, 0);

                    clients.Remove(clientName);
                    cliensNames.Remove(clientName);

                    SocketLogger.Log($"Server: Client {clientName} disconnected");
                }
            }
        }
    }
}
