using NetMQ;
using NetMQ.Sockets;
using RSMA.uDTP;
using RSMA.uDTP.CDR;
using RSMA.uDTP.Topics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RSMA.NetMQ
{
    public static class NetMQServer
    {
        private static bool _isRunning;
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();

        private static RouterSocket _routerSocket;
        private static PublisherSocket _pubSocket;
        private static SubscriberSocket _subSocket;
        private static uint _globalSequence = 0;

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
            if (_isRunning) return;
            _isRunning = true;

            // Запуск потока служебных команд (ROUTER)
            Task.Run(() => CommandServerLoop(commandPort));

            // Инициализация сокета публикаций (PUB)
            Task.Run(() => InitPublisher(pubPort));

            Task.Run(() => ReceiverLoop(subPort));
        }

        public static void Stop()
        {
            _isRunning = false;
            _routerSocket?.Dispose();
            _pubSocket?.Dispose();
            _subSocket?.Dispose();
            NetMQConfig.Cleanup();
        }

        // =========================================================
        // 1. ИНИЦИАЛИЗАЦИЯ И ПУБЛИКАЦИЯ ВЫСОКОЧАСТОТНЫХ ДАННЫХ (PUB)
        // =========================================================

        private static void InitPublisher(int pubPort)
        {
            AsyncIO.ForceDotNet.Force();
            _pubSocket = new PublisherSocket();
            _pubSocket.Options.SendHighWatermark = 1000;
            _pubSocket.Options.Linger = TimeSpan.Zero;
            _pubSocket.Bind($"tcp://*:{pubPort}");
        }


        private static void ReceiverLoop(int subPort)
        {
            AsyncIO.ForceDotNet.Force();
            using (_subSocket = new SubscriberSocket())
            {
                _subSocket.Options.Linger = TimeSpan.Zero;
                _subSocket.Options.ReceiveHighWatermark = 1000;

                // ВМЕСТО Connect ДЕЛАЕМ Bind:
                _subSocket.Bind($"tcp://*:{subPort}");
                _subSocket.Subscribe(""); // Подписка на все топики

                while (_isRunning)
                {
                    var message = _subSocket.ReceiveMultipartMessage();

                    if (message.FrameCount >= 3)
                    {
                        string topicName = message[0].ConvertToString();
                        byte[] headerBytes = message[1].ToByteArray();
                        byte[] payloadBytes = message[2].ToByteArray();

                        DataBroker.UpdateFromNetwork(topicName, headerBytes, payloadBytes);
                    }
                }
            }
        }

        /// <summary>
        /// Быстрая публикация любых blittable/value-структур в CDR через NetMQ
        /// </summary>
        /// <summary>
        /// Быстрая публикация любых blittable/value-структур в CDR через NetMQ
        /// </summary>
        public static void PublishCdrTopic<T>(string topicName, in T data) where T : struct
        {
            if (!_isRunning || _pubSocket == null) return;

            unsafe
            {
                int payloadSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                int headerSize = System.Runtime.CompilerServices.Unsafe.SizeOf<TopicHeader>();

                byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(headerSize);
                byte[] payloadBuffer = ArrayPool<byte>.Shared.Rent(payloadSize);

                try
                {
                    // 1. Упаковка данных в CDR
                    CdrSerializer.Pack(data, payloadBuffer);

                    // 2. Формирование единого заголовка
                    var header = new TopicHeader
                    {
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        sequence = _globalSequence++,
                        payloadSize = payloadSize,
                        topicTypeId = typeof(T).GetHashCode()
                    };
                    CdrSerializer.Pack(header, headerBuffer);

                    // 3. Формирование и отправка мультипарт-сообщения NetMQ
                    var msg = new NetMQMessage();

                    // Frame 0: Имя топика (string)
                    msg.Append(topicName);

                    // Frame 1: Заголовок (создаем явный NetMQFrame с указанием длины)
                    msg.Append(new NetMQFrame(headerBuffer, headerSize));

                    // Frame 2: Полезная нагрузка (создаем явный NetMQFrame с указанием длины)
                    msg.Append(new NetMQFrame(payloadBuffer, payloadSize));

                    _pubSocket.SendMultipartMessage(msg);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(headerBuffer);
                    ArrayPool<byte>.Shared.Return(payloadBuffer);
                }
            }
        }

        /// <summary>
        /// Публикация сырых бинарных массивов (кадры камер, сканы лидара)
        /// </summary>
        public static void PublishRaw(string topicName, byte[] rawData, int typeId = 0)
        {
            if (!_isRunning || _pubSocket == null || rawData == null) return;

            unsafe
            {
                int headerSize = System.Runtime.CompilerServices.Unsafe.SizeOf<TopicHeader>();
                byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(headerSize);

                try
                {
                    var header = new TopicHeader
                    {
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        sequence = _globalSequence++,
                        payloadSize = rawData.Length,
                        topicTypeId = typeId
                    };
                    CdrSerializer.Pack(header, headerBuffer);

                    var msg = new NetMQMessage();

                    // Frame 0: Имя топика
                    msg.Append(topicName);

                    // Frame 1: Заголовок
                    msg.Append(new NetMQFrame(headerBuffer, headerSize));

                    // Frame 2: Сырые данные
                    msg.Append(new NetMQFrame(rawData, rawData.Length));

                    _pubSocket.SendMultipartMessage(msg);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(headerBuffer);
                }
            }
        }

        // =========================================================
        // 2. СЕРВЕР СЛУЖЕБНЫХ КОМАНД (ROUTER SOCKET)
        // =========================================================

        private static void CommandServerLoop(int commandPort)
        {
            AsyncIO.ForceDotNet.Force();
            using (_routerSocket = new RouterSocket())
            {
                _routerSocket.Bind($"tcp://*:{commandPort}");

                while (_isRunning)
                {
                    var message = _routerSocket.ReceiveMultipartMessage();

                    if (message.FrameCount >= 3)
                    {
                        var clientIdentity = message[0];
                        byte[] rawBytes = message[2].ToByteArray();
                        string payload = Encoding.UTF8.GetString(rawBytes).Trim();

                        string response = ProcessCommand(payload);
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                        _routerSocket.SendMultipartMessage(new NetMQMessage(new[]
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