using UnityEngine;
using UnityEngine.SceneManagement;

public static class CommandHandler
{
    public static Terminal terminal;

    public static RSMAServer server;

    public static ObjectManager objectManager;

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
                                server = new RSMAServer();

                                server.serverIP = "127.0.0.1";
                                server.serverPort = 7777;

                                server.Run();
                                return "Starting server on 127.0.0.1:7777";
                            case "stop":
                                if (server != null)
                                {
                                    server.Stop();
                                }
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

                case "scene":
                    if(splited.Length > 2 && splited[1] == "load")
                    {
                        SceneManager.LoadScene(splited[2]);
                        return $"Loading scene {splited[2]}";
                    }
                    return "Invalid argument for scene";

                case "marker":
                    if (splited.Length > 4)
                    {
                        string name = splited[1];
                        float x = float.Parse(splited[2]);
                        float y = float.Parse(splited[3]);
                        float z = float.Parse(splited[4]);

                        objectManager.InstantiateMarker(splited[1], new Vector3(x, y, z));
                        return $"Done";
                    }
                    return "Invalid argument for marker";

                case "wall":
                    if (splited.Length > 8)
                    {
                        float x = float.Parse(splited[1]);
                        float y = float.Parse(splited[2]);
                        float z = float.Parse(splited[3]);
                        Vector3 start = new Vector3(x, y, z);

                        x = float.Parse(splited[4]);
                        y = float.Parse(splited[5]);
                        z = float.Parse(splited[6]);
                        Vector3 end = new Vector3(x, y, z);

                        float height = float.Parse(splited[7]);
                        float width = float.Parse(splited[8]);

                        objectManager.InstantiateWall(start, end, height, width);
                        return $"Done";
                    }
                    return "Invalid argument for marker";

                case "robot":
                    if (splited.Length > 7)
                    {

                        string name = splited[1];
                        float x = float.Parse(splited[2]);
                        float y = float.Parse(splited[3]);
                        float z = float.Parse(splited[4]);
                        Vector3 position = new Vector3(x, y, z);

                        x = float.Parse(splited[5]);
                        y = float.Parse(splited[6]);
                        z = float.Parse(splited[7]);
                        Vector3 rotation = new Vector3(x, y, z);

                        objectManager.InstantiateRobot(name, position, rotation);
                        return $"Done";
                    }
                    return "Invalid argument for robot";

                case "gpio_write":
                    if (splited.Length > 4)
                    {
                        int id = int.Parse(splited[1]);
                        string port = splited[2];
                        string pin = splited[3];
                        float value = float.Parse(splited[4]);

                        objectManager.GPIOWrite(id, port, pin, value);  
                        return $"Done";
                    }
                    return "Invalid argument for gpioWrite";

                case "gpio_read":
                    if (splited.Length > 3)
                    {
                        int id = int.Parse(splited[1]);
                        string port = splited[2];
                        string pin = splited[3];

                        float value = objectManager.GPIORead(id, port, pin);
                        return value.ToString();
                    }
                    return "Invalid argument for gpioRead";

                case "drone":
                    if (splited.Length > 3)
                    {
                        float x = float.Parse(splited[1]);
                        float y = float.Parse(splited[2]);
                        float z = float.Parse(splited[3]);
                        Vector3 position = new Vector3(x, y, z);

                        objectManager.InstantiateDrone(position);
                        return $"Done";
                    }
                    return "Invalid argument for drone";

                case "drone_move":
                    if (splited.Length > 5)
                    {
                        int id = int.Parse(splited[1]);
                        float x = float.Parse(splited[2]);
                        float y = float.Parse(splited[3]);
                        float z = float.Parse(splited[4]);
                        float yaw = float.Parse(splited[5]);
                        Vector3 acceleration = new Vector3(x, y, z);

                        objectManager.DroneMove(id, acceleration, yaw);
                        return $"Done";
                    }
                    return "Invalid argument for drone_move";

                case "drone_camera":
                    if (splited.Length > 5)
                    {
                        int id = int.Parse(splited[1]);
                        float x = float.Parse(splited[2]);
                        float y = float.Parse(splited[3]);
                        float z = float.Parse(splited[4]);
                        float smooth = float.Parse(splited[5]);
                        Vector3 rotation = new Vector3(x, y, z);

                        objectManager.DroneCamera(id, rotation, smooth);
                        return $"Done";
                    }
                    return "Invalid argument for drone_camera";

                case "drone_manual_control":
                    if (splited.Length > 2)
                    {
                        int id = int.Parse(splited[1]);
                        bool mode = bool.Parse(splited[2]);

                        objectManager.DroneManualControl(id, mode);
                        return $"Done";
                    }
                    return "Invalid argument for drone_manual_control";
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

/*
                case "command":
                    if(splited.Length > 3 && splited[1] == "load")
                    {
                        SceneManager.LoadScene(splited[2]);
                        return $"Loading scene {splited[2]}";
                    }
                    return "Invalid argument for command";
*/