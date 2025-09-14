using System.Net.NetworkInformation;

public class RSMAServer : SocketServer
{
    public override void OnClientConnected(string clientName)
    {
        if (CommandHandler.terminal != null)
        {
            CommandHandler.terminal.Print($"\nClient {clientName} connected to server");
        }
    }
    public override void OnClientDisconnected(string clientName)
    {
        if (CommandHandler.terminal != null)
        {
            CommandHandler.terminal.Print($"\nClient {clientName} disconnected from server");
        }
    }
    public override void OnMessageRecived(string clientName, string message)
    {
        if (CommandHandler.terminal != null)
        {
            if (message.Contains("<|CMD|>"))
            {
                message = message.Replace("<|CMD|>", "");
                string reply = CommandHandler.Execute(message);
                SendMessageToClientAsync(clientName, reply); 
            }
            else
            {
                CommandHandler.terminal.Print($"\nRecived message from {clientName}: {message}");
            }
        }
    }
}