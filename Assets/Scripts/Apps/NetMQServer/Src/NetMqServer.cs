using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RSMA.NetMQ
{
    public static class NetMQServer
    {
        private static bool _isRunning;
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        public static bool IsRunning => _isRunning;

        static NetMQServer()
        {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                {
                    Stop();
                }
            };
        #endif

            Application.quitting += () =>
            {
                Stop();
            };
        }

        public static void Run(int serverPort = 5555)
        {
            if (_isRunning) return;
            _isRunning = true;

            Task.Run(() => ServerLoop(serverPort));
        }

        public static void Stop()
        {
            _isRunning = false;
            NetMQConfig.Cleanup();
        }

        private static void ServerLoop(int serverPort)
        {
            AsyncIO.ForceDotNet.Force();
            using (var server = new RouterSocket())
            {
                server.Bind($"tcp://*:{serverPort}");

                while (_isRunning)
                {
                    // Router сообщение в формате: [Identity, EmptyFrame, Data]
                    var message = server.ReceiveMultipartMessage();

                    if (message.FrameCount >= 3)
                    {
                        var clientIdentity = message[0];
                        byte[] rawBytes = message[2].ToByteArray();
                        string payload = Encoding.UTF8.GetString(rawBytes).Trim();

                        string response = ProcessCommand(payload);
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                        // Отправляем ответ обратно тому же клиенту
                        server.SendMultipartMessage(new NetMQMessage(new[] 
                        { 
                            clientIdentity, 
                            NetMQFrame.Empty, 
                            new NetMQFrame(responseBytes) 
                        }));
                    }
                }
            }
            NetMQConfig.Cleanup();
        }

        private static string ProcessCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "{\"status\":\"error\",\"message\":\"Empty payload\"}";

            if (command.Trim().StartsWith("{") && command.Trim().EndsWith("}"))
            {
                try
                {
                    var packet = JsonConvert.DeserializeObject<NetworkPacket>(command);

                    if (packet != null && !string.IsNullOrEmpty(packet.Action) && !string.IsNullOrEmpty(packet.TopicType))
                    {
                        return ProcessBrokerCommand(packet);
                    }
                }
                catch
                {
                    // Если десериализация упала, значит это был не пакет брокера, 
                    // либо JSON поврежден. Идем дальше к обычным командам.
                }
            }

            if (command.StartsWith("PrintMessage:"))
            {
                string msg = command.Substring("PrintMessage:".Length);
                EnqueueAction(() => Debug.Log($"Message: {msg}"));
                return "OK: Message printed";
            }
            else if (command.StartsWith("RestartLevel"))
            {
                EnqueueAction(() =>
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                    );
                });
                return "OK: Level restarting";
            }
            else if (command.StartsWith("GetServerStatus"))
            {
                return "OK: Server is running";
            }
            else 
            {
                return $"Error: Unknown command '{command}'";
            }


        }

        // Потокобезопасная очередь для выполнения в основном потоке Unity
        public static void EnqueueAction(Action action)
        {
            lock (_queueLock) { _actionQueue.Enqueue(action); }
        }

        // Метод, который нужно вызывать в Update любого MonoBehaviour
        public static void Update()
        {
            lock (_queueLock)
            {
                while (_actionQueue.Count > 0)
                {
                    _actionQueue.Dequeue().Invoke();
                }
            }
        }

        private static string ProcessBrokerCommand(NetworkPacket packet)
        {
            try
            {
                string fullTypeName = "RSMA.uDTP.Topics." + packet.TopicType;
                Type topicType = Type.GetType(fullTypeName);

                if (topicType == null)
                {
                    return $"{{\"status\":\"error\",\"message\":\"Type '{packet.TopicType}'\"}}";
                }

                if (packet.Action == "publish")
                {
                    object deserializedData = JsonConvert.DeserializeObject(packet.Data, topicType, JsonSettings);

                    // Получаем generic-метод DataBroker.Publish<T>
                    MethodInfo publishMethod = typeof(RSMA.uDTP.DataBroker)
                        .GetMethod("Publish", BindingFlags.Public | BindingFlags.Static)
                        .MakeGenericMethod(topicType);

                    // DataBroker.Publish<TargetType>(packet.TopicName, deserializedData)
                    publishMethod.Invoke(null, new object[] { packet.TopicName, deserializedData });

                    return "{\"status\":\"ok\"}";
                }

                else if (packet.Action == "get")
                {
                    // Через рефлексию получаем generic-метод DataBroker.GetState<T>
                    MethodInfo getMethod = typeof(RSMA.uDTP.DataBroker)
                        .GetMethod("GetState", BindingFlags.Public | BindingFlags.Static)
                        .MakeGenericMethod(topicType);

                    // Вызываем: DataBroker.GetState<TargetType>(packet.TopicName)
                    object state = getMethod.Invoke(null, new object[] { packet.TopicName });

                    // Сериализуем полученный объект (даже если он default/null) back to JSON
                    string dataJson = JsonConvert.SerializeObject(state, JsonSettings);
                    return JsonConvert.SerializeObject(new NetworkResponse { Status = "ok", Data = dataJson }, JsonSettings);
                }
            }
            catch (Exception ex)
            {
                // Если ошибка произошла внутри Invoke, реальное исключение будет в InnerException
                string errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return $"{{\"status\":\"error\",\"message\":\"Reflection error: {errorMsg}\"}}";
            }
            return $"{{\"status\":\"error\",\"message\":\"Type 'Unknown'\"}}";
        }
    }
}