using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CommandHandler
{
    public static Terminal terminal;

    public static RSMAServer server;

    public static ObjectManager objectManager;

    public static string Execute(string command)
    {
        command = command.Replace(',', '.');

        var splited = command.Split(' ');


        if (splited.Length > 0 && splited[0] != "") 
        {
            switch (splited[0])
            {
                case "shutdown":
                    Application.Quit();
                    return "Shutting down RSMA";

                case "help":
                    Application.OpenURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/en/Utilities/TerminalCommands.md");
                    return "RSMA trying to help and recommends reading the documentation on https://github.com/GrimDarkTech/RSMADocs";

                case "server_start":
                    server = new RSMAServer();

                    server.serverIP = "127.0.0.1";
                    server.serverPort = 7777;

                    server.Run();
                    return "Starting server on 127.0.0.1:7777";

                case "server_stop":
                    if (server != null)
                    {
                        server.Stop();
                    }
                    return "Stopping server";

                case "server_send":
                    if (splited.Length > 1)
                    {
                        string client = splited[1];
                        string message = splited[2];

                        server.SendMessageToClientAsync(client, message);
                        return "Sending";
                    }
                    return "Invalid argument for server_send";

                case "scene_load":
                    if(splited.Length > 1)
                    {
                        string scene = splited[1];
                        SceneManager.LoadScene(scene);
                        return $"Loading scene {scene}";
                    }
                    return "Invalid argument for scene_load";

                case "marker":
                    if (splited.Length > 4)
                    {
                        string name = splited[1];
                        float x = float.Parse(splited[2], CultureInfo.InvariantCulture);
                        float y = float.Parse(splited[3], CultureInfo.InvariantCulture);
                        float z = float.Parse(splited[4], CultureInfo.InvariantCulture);

                        objectManager.InstantiateMarker(splited[1], new Vector3(x, y, z));
                        return $"Done";
                    }
                    return "Invalid argument for marker";

                case "wall":
                    if (splited.Length > 8)
                    {
                        float x = float.Parse(splited[1], CultureInfo.InvariantCulture);
                        float y = float.Parse(splited[2], CultureInfo.InvariantCulture);
                        float z = float.Parse(splited[3], CultureInfo.InvariantCulture);
                        Vector3 start = new Vector3(x, y, z);

                        x = float.Parse(splited[4], CultureInfo.InvariantCulture);
                        y = float.Parse(splited[5], CultureInfo.InvariantCulture);
                        z = float.Parse(splited[6], CultureInfo.InvariantCulture);
                        Vector3 end = new Vector3(x, y, z);

                        float height = float.Parse(splited[7], CultureInfo.InvariantCulture);
                        float width = float.Parse(splited[8], CultureInfo.InvariantCulture);

                        objectManager.InstantiateWall(start, end, height, width);
                        return $"Done";
                    }
                    return "Invalid argument for marker";

                case "robot":
                    if (splited.Length > 7)
                    {

                        string name = splited[1];
                        float x = float.Parse(splited[2], CultureInfo.InvariantCulture);
                        float y = float.Parse(splited[3], CultureInfo.InvariantCulture);
                        float z = float.Parse(splited[4], CultureInfo.InvariantCulture);
                        Vector3 position = new Vector3(x, y, z);

                        x = float.Parse(splited[5], CultureInfo.InvariantCulture);
                        y = float.Parse(splited[6], CultureInfo.InvariantCulture);
                        z = float.Parse(splited[7], CultureInfo.InvariantCulture);
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
                        float value = float.Parse(splited[4], CultureInfo.InvariantCulture);

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
                        return  $"<|GPIO|>{id}<|s|>{port}<|s|>{pin}<|s|>{value}";
                    }
                    return "Invalid argument for gpio_read";

                case "drone":
                    if (splited.Length > 3)
                    {
                        float x = float.Parse(splited[1], CultureInfo.InvariantCulture);
                        float y = float.Parse(splited[2], CultureInfo.InvariantCulture);
                        float z = float.Parse(splited[3], CultureInfo.InvariantCulture);
                        Vector3 position = new Vector3(x, y, z);

                        objectManager.InstantiateDrone(position);
                        return $"Done";
                    }
                    return "Invalid argument for drone";

                case "drone_acceleration":
                    if (splited.Length > 4)
                    {
                        int id = int.Parse(splited[1]);
                        float x = float.Parse(splited[2], CultureInfo.InvariantCulture);
                        float y = float.Parse(splited[3], CultureInfo.InvariantCulture);
                        float z = float.Parse(splited[4], CultureInfo.InvariantCulture);
                        Vector3 acceleration = new Vector3(x, y, z);

                        objectManager.DroneSetAcceleration(id, acceleration);
                        return $"Done";
                    }
                    return "Invalid argument for drone_acceleration";

                case "drone_camera":
                    if (splited.Length > 5)
                    {
                        int id = int.Parse(splited[1]);
                        float x = float.Parse(splited[2], CultureInfo.InvariantCulture);
                        float y = float.Parse(splited[3], CultureInfo.InvariantCulture);
                        float z = float.Parse(splited[4], CultureInfo.InvariantCulture);
                        float smooth = float.Parse(splited[5], CultureInfo.InvariantCulture);
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

                case "drone_move":
                    if (splited.Length > 7)
                    {
                        int id = int.Parse(splited[1]);
                        float x = float.Parse(splited[2], CultureInfo.InvariantCulture);
                        float y = float.Parse(splited[3], CultureInfo.InvariantCulture);
                        float z = float.Parse(splited[4], CultureInfo.InvariantCulture);
                        float kp = float.Parse(splited[5], CultureInfo.InvariantCulture);
                        float ki = float.Parse(splited[6], CultureInfo.InvariantCulture);
                        float kd = float.Parse(splited[7], CultureInfo.InvariantCulture);
                        Vector3 targetPosition = new Vector3(x, y, z);

                        objectManager.DroneMoveToPosition(id, targetPosition, kp, ki, kd);
                        return $"Done";
                    }
                    return "Invalid argument for drone_move";

                case "drone_switch_camera":
                    if (splited.Length > 1)
                    {
                        int id = int.Parse(splited[1]);

                        objectManager.DroneSwitch(id);
                        return $"Done";
                    }
                    return "Invalid argument for drone_switch_camera";

                case "writer_start":
                    if (splited.Length > 1)
                    {
                        int id = int.Parse(splited[1]);

                        objectManager.WriterStart(id);
                        return $"Done";
                    }
                    return "Invalid argument for writer_start";

                case "writer_stop":
                    if (splited.Length > 1)
                    {
                        int id = int.Parse(splited[1]);

                        objectManager.WriterStop(id);
                        return $"Done";
                    }
                    return "Invalid argument for writer_stop";

                case "controller_position":
                    if (splited.Length > 1)
                    {
                        int id = int.Parse(splited[1]);

                        Transform transform = objectManager.GetTransform(id);
                        Vector3 position = transform.position;
                        Vector3 rotation = transform.rotation.eulerAngles;

                        return $"<|Transform|>{id}<|s|>{position.x}<|s|>{position.y}<|s|>{position.z}" +
                            $"<|s|>{rotation.x}<|s|>{rotation.y}<|s|>{rotation.z}";
                    }
                    return "Invalid argument for controller_position";

                case "trails_start":
                    if (splited.Length > 1)
                    {
                        int id = int.Parse(splited[1]);

                        objectManager.TrailsStart(id);
                        return $"Done";
                    }
                    return "Invalid argument for trails_start";

                case "trails_stop":
                    if (splited.Length > 1)
                    {
                        int id = int.Parse(splited[1]);

                        objectManager.TrailsStop(id);
                        return $"Done";
                    }
                    return "Invalid argument for trails_stop";

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