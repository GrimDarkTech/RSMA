using UnityEngine;

public static class CommandHandler
{
    public static Terminal terminal;

    public static RSMAServer server;

    public static void Start(string serverIP, int serverPort)
    {
        RSMALogger.Logger.backend = RSMALogger.Backends.Unity;

        server = new RSMAServer();

        server.serverIP = serverIP;
        server.serverPort = serverPort;

        server.Run();
    }
    public static void Stop()
    {
        if (server != null)
        {
            server.Stop();
        }
    }

    public static string Execute(string command)
    {
        var splited = command.Split(' ');

        if (splited.Length > 0 && splited[0] != "") 
        {
            switch (splited[0])
            {
                case "shutdown":
                    Application.Quit();
                    return "Shutting down RSMA";
                case "help":
                    Application.OpenURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/en/TerminalCommands.md");
                    return "RSMA trying to help and recommends reading the documentation on https://github.com/GrimDarkTech/RSMADocs";
                case "server":
                    if(splited.Length > 1)
                    {
                        switch (splited[1])
                        {
                            case "start":
                                Start("127.0.0.1", 7777);
                                return "Starting server on 127.0.0.1:7777";
                            case "stop":
                                Stop();
                                return "Stopping server";
                            case "send":
                                if(splited.Length > 3)
                                {
                                    server.SendMessageToClientAsync(splited[2], splited[3]);
                                    return "Sending";
                                }
                                else
                                {
                                    return "Invalid argument for server send";
                                }
                            default:
                                return "Invalid argument for server";
                        }
                    }
                    else
                    {
                        return "Invalid argument for server";
                    }
                default:
                    return "Invalid command";
            }
        }
        else
        {
            return "Invalid command";
        }
    }
}



