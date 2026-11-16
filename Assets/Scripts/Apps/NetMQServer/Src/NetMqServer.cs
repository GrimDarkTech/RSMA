using NetMQ;
using NetMQ.Sockets;
using RSMA.uDTP;
using RSMA.uDTP.CDR;
using RSMA.uDTP.Topics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Pose = RSMA.uDTP.Topics.Pose;

namespace RSMA.NetMQ
{
    public static class NetMQServer
    {
        private static volatile bool _isRunning;
        private static readonly object _lifecycleLock = new object();
        private static Task[] _serverTasks = Array.Empty<Task>();
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();

        private static RouterSocket _routerSocket;
        private static PublisherSocket _pubSocket;
        private static SubscriberSocket _subSocket;
        private static uint _globalSequence = 0;
        private static readonly object _publishLock = new object();

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
            Application.quitting += () => Stop();
        }

        public static void Run(int commandPort = 5555, int pubPort = 5556, int subPort = 5560)
        {
            lock (_lifecycleLock)
            {
                if (_isRunning) return;
                _isRunning = true;

                // Keep task handles so Stop can complete the previous generation before a restart.
                _serverTasks = new[]
                {
                    Task.Run(() => CommandServerLoop(commandPort)),
                    Task.Run(() => InitPublisher(pubPort)),
                    Task.Run(() => ReceiverLoop(subPort))
                };
            }
        }

        public static void Stop()
        {
            Task[] tasks;
            lock (_lifecycleLock)
            {
                if (!_isRunning && _serverTasks.Length == 0) return;
                _isRunning = false;
                tasks = _serverTasks;
                _serverTasks = Array.Empty<Task>();

                _routerSocket?.Dispose();
                _pubSocket?.Dispose();
                _subSocket?.Dispose();
                _routerSocket = null;
                _pubSocket = null;
                _subSocket = null;
            }

            try
            {
                Task.WaitAll(tasks, TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Socket disposal interrupts receive loops during normal shutdown.
            }
            NetMQConfig.Cleanup();
        }

        // =========================================================
        // 1. ИНИЦИАЛИЗАЦИЯ И ПУБЛИКАЦИЯ ВЫСОКОЧАСТОТНЫХ ДАННЫХ (PUB)
        // =========================================================

        private static void InitPublisher(int pubPort)
        {
            AsyncIO.ForceDotNet.Force();
            var socket = new PublisherSocket();
            try
            {
                socket.Options.SendHighWatermark = 1000;
                socket.Options.Linger = TimeSpan.Zero;
                socket.Bind($"tcp://*:{pubPort}");
                if (!_isRunning)
                {
                    socket.Dispose();
                    return;
                }
                _pubSocket = socket;
            }
            catch
            {
                socket.Dispose();
                if (_isRunning) _isRunning = false;
                throw;
            }
        }


        private static void ReceiverLoop(int subPort)
        {
            AsyncIO.ForceDotNet.Force();
            using (var socket = new SubscriberSocket())
            {
                socket.Options.Linger = TimeSpan.Zero;
                socket.Options.ReceiveHighWatermark = 1000;

                // ВМЕСТО Connect ДЕЛАЕМ Bind:
                socket.Bind($"tcp://*:{subPort}");
                if (!_isRunning)
                    return;
                _subSocket = socket;
                socket.Subscribe(""); // Подписка на все топики

                while (_isRunning)
                {
                    NetMQMessage message = new NetMQMessage();
                    if (!socket.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(100), ref message))
                        continue;

                    if (message.FrameCount == 3)
                    {
                        try
                        {
                            string topicName = message[0].ConvertToString();
                            byte[] headerBytes = message[1].ToByteArray();
                            byte[] payloadBytes = message[2].ToByteArray();
                            DataBroker.UpdateFromNetwork(topicName, headerBytes, payloadBytes);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Rejected invalid uDTP packet: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// <summary>
        /// Быстрая публикация любых blittable/value-структур в CDR через NetMQ
        /// </summary>
        public static void PublishCdrTopic<T>(string topicName, in T data) where T : struct
        {
            if (!_isRunning || _pubSocket == null) return;
            if (!UdtpV1Codec.TryGetTypeId(typeof(T), out var typeId))
            {
                // Keep unsupported typed payloads opaque until a stable v1 codec is assigned.
                PublishOpaque(topicName, data);
                return;
            }

            byte[] payload = UdtpV1Codec.Encode(data);
            PublishV1(topicName, typeId, payload, GetPayloadTimestamp(data));
        }

        /// <summary>
        /// Публикация сырых бинарных массивов (кадры камер, сканы лидара)
        /// </summary>
        public static void PublishRaw(string topicName, byte[] rawData, int typeId = 0)
        {
            if (!_isRunning || _pubSocket == null || rawData == null) return;
            var header = new TopicHeader
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sequence = NextSequence(),
                payloadSize = rawData.Length,
                topicTypeId = typeId
            };
            SendMultipart(topicName, header.EncodeV1(), rawData);
        }

        /// <summary>
        /// Публикация legacy raw camera payload с сохранённым ID 2.
        /// </summary>
        public static void PublishCameraRaw(string topicName, byte[] rawData)
        {
            PublishRaw(topicName, rawData, UdtpV1Codec.LegacyCameraTypeId);
        }

        private static void PublishV1(string topicName, int typeId, byte[] payload, long payloadTimestamp)
        {
            if (payload.Length > int.MaxValue) return;
            var header = new TopicHeader
            {
                timestamp = payloadTimestamp > 0 ? payloadTimestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sequence = NextSequence(),
                payloadSize = payload.Length,
                topicTypeId = typeId
            };
            SendMultipart(topicName, header.EncodeV1(), payload);
        }

        private static void PublishOpaque<T>(string topicName, in T data) where T : struct
        {
            int payloadSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            byte[] payload = new byte[payloadSize];
            CdrSerializer.Pack(data, payload);
            var header = new TopicHeader
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sequence = NextSequence(),
                payloadSize = payloadSize,
                topicTypeId = UdtpV1Codec.OpaqueTypeId
            };
            SendMultipart(topicName, header.EncodeV1(), payload);
        }

        private static uint NextSequence()
        {
            lock (_publishLock)
                return _globalSequence++;
        }

        private static long GetPayloadTimestamp<T>(in T value) where T : struct
        {
            if (value is RobotVelocity velocity) return velocity.timestamp;
            if (value is Pose pose) return pose.timestamp;
            if (value is LaserScan128 scan) return scan.timestamp;
            return 0;
        }

        private static void SendMultipart(string topicName, byte[] header, byte[] payload)
        {
            lock (_publishLock)
            {
                if (!_isRunning || _pubSocket == null) return;
                var message = new NetMQMessage();
                message.Append(topicName);
                message.Append(header);
                message.Append(payload);
                _pubSocket.SendMultipartMessage(message);
            }
        }

        // =========================================================
        // 2. СЕРВЕР СЛУЖЕБНЫХ КОМАНД (ROUTER SOCKET)
        // =========================================================

        private static void CommandServerLoop(int commandPort)
        {
            AsyncIO.ForceDotNet.Force();
            using (var socket = new RouterSocket())
            {
                socket.Bind($"tcp://*:{commandPort}");
                if (!_isRunning)
                    return;
                _routerSocket = socket;

                while (_isRunning)
                {
                    NetMQMessage message = new NetMQMessage();
                    if (!socket.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(100), ref message))
                        continue;

                    if (message.FrameCount >= 3)
                    {
                        var clientIdentity = message[0];
                        byte[] rawBytes = message[2].ToByteArray();
                        string payload = Encoding.UTF8.GetString(rawBytes).Trim();

                        string response = ProcessCommand(payload);
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                        socket.SendMultipartMessage(new NetMQMessage(new[]
                        {
                            clientIdentity,
                            NetMQFrame.Empty,
                            new NetMQFrame(responseBytes)
                        }));
                    }
                }
            }
        }

        private static string ProcessCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "{\"status\":\"error\",\"message\":\"Empty payload\"}";

            if (command.StartsWith("PrintMessage:"))
            {
                string msg = command.Substring("PrintMessage:".Length);
                EnqueueAction(() => Debug.Log($"Message: {msg}"));
                return "OK: Message printed";
            }
            if (command.Equals("GetTopics", StringComparison.OrdinalIgnoreCase))
            {
                return DataBroker.GetTopicsJson();
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

        // =========================================================
        // 3. ДЕЛЕГИРОВАНИЕ В ГЛАВНЫЙ ПОТОК UNITY
        // =========================================================

        public static void EnqueueAction(Action action)
        {
            lock (_queueLock) { _actionQueue.Enqueue(action); }
        }

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
    }
}