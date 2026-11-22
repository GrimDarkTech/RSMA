using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using RSMA.uDTP;
using RSMA.uDTP.CDR;
using RSMA.uDTP.Topics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Pose = RSMA.uDTP.Topics.Pose;

namespace RSMA.NetMQ
{
    public static class NetMQServer
    {
        private static readonly object _lifecycleLock = new object();
        private static volatile ServerSession _session;
        private static Task _stopTask;
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();
        private static readonly TimeSpan _stopTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan _socketTimeout = TimeSpan.FromMilliseconds(100);
        private static uint _globalSequence;
        private static readonly object _publishLock = new object();

        public static bool IsRunning => _session?.IsRunning == true;
        public static bool IsReady => _session?.IsReady == true;

        private sealed class ServerSession
        {
            private readonly TaskCompletionSource<bool> _stopping =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _boundEndpoints;

            public Task Stopping => _stopping.Task;
            public bool IsRunning => !Stopping.IsCompleted;
            public bool IsReady => Volatile.Read(ref _boundEndpoints) == 3 && IsRunning;
            public Task Completion { get; set; }
            public PublisherSocket Publisher { get; set; }

            public void EndpointBound() => Interlocked.Increment(ref _boundEndpoints);
            public void RequestStop() => _stopping.TrySetResult(true);
        }

        static NetMQServer()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                    RequestStopInBackground();
            };
#endif
            Application.quitting += RequestStopInBackground;
        }

        private static void RequestStopInBackground() => StopInBackground();

        public static Task StopInBackground()
        {
            lock (_lifecycleLock)
            {
                var session = _session;
                if (session == null)
                    return Task.CompletedTask;
                session.RequestStop();
                if (_stopTask != null && !_stopTask.IsCompleted)
                    return _stopTask;
                _stopTask = StopSession(session);
                return _stopTask;
            }
        }

        public static void Run(int commandPort = 5555, int pubPort = 5556, int subPort = 5560)
        {
            ValidatePort(commandPort, nameof(commandPort));
            ValidatePort(pubPort, nameof(pubPort));
            ValidatePort(subPort, nameof(subPort));
            if (commandPort == pubPort || commandPort == subPort || pubPort == subPort)
                throw new ArgumentException("NetMQ server ports must be different.");

            lock (_lifecycleLock)
            {
                if (_session?.IsRunning == true)
                    return;
                if (_session != null && !_session.Completion.IsCompleted)
                    throw new InvalidOperationException("The previous NetMQ server session is still stopping; refusing to bind another generation.");

                var session = new ServerSession();
                _session = session;
                session.Completion = Task.WhenAll(
                    StartEndpoint(session, "command", commandPort, () => new RouterSocket(), socket =>
                    {
                        CommandServerLoop(session, socket);
                        return Task.CompletedTask;
                    }),
                    StartEndpoint(session, "publish", pubPort, () => new PublisherSocket(), socket => PublisherLoop(session, socket)),
                    StartEndpoint(session, "subscribe", subPort, () => new SubscriberSocket(), socket =>
                    {
                        ReceiverLoop(session, socket);
                        return Task.CompletedTask;
                    }));
            }
        }

        private static void ValidatePort(int port, string parameterName)
        {
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(parameterName, port, "NetMQ server ports must be between 1 and 65535.");
        }

        public static void Stop()
        {
            ServerSession session;
            lock (_lifecycleLock)
            {
                session = _session;
                if (session == null)
                    return;
                session.RequestStop();
            }
            StopSessionCore(session);
        }

        private static Task StopSession(ServerSession session)
        {
            return Task.Run(() => StopSessionCore(session));
        }

        private static void StopSessionCore(ServerSession session)
        {
            if (!session.Completion.Wait(_stopTimeout))
            {
                Debug.LogError("NetMQ server did not stop within two seconds; restart is blocked until its endpoints exit.");
                return;
            }
            lock (_lifecycleLock)
            {
                if (_session == session)
                    _session = null;
            }
        }

        private static Task StartEndpoint<TSocket>(ServerSession session, string name, int port,
            Func<TSocket> createSocket, Func<TSocket, Task> run) where TSocket : NetMQSocket
        {
            return Task.Run(async () =>
            {
                if (!session.IsRunning)
                    return;
                try
                {
                    AsyncIO.ForceDotNet.Force();
                    using (var socket = createSocket())
                    {
                        socket.Options.Linger = TimeSpan.Zero;
                        if (socket is PublisherSocket) socket.Options.SendHighWatermark = 1000;
                        if (socket is SubscriberSocket) socket.Options.ReceiveHighWatermark = 1000;

                        var endpoint = $"tcp://*:{port}";
                        var monitorEndpoint = $"inproc://rsma-close-{Guid.NewGuid():N}";
                        socket.Monitor(monitorEndpoint, SocketEvents.Closed | SocketEvents.CloseFailed);
                        using (var events = new PairSocket())
                        using (var monitor = new NetMQMonitor(events, monitorEndpoint, false))
                        {
                            events.Options.Linger = TimeSpan.Zero;
                            var monitorTask = monitor.StartAsync();
                            var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            monitor.Closed += (_, __) => closed.TrySetResult(true);
                            monitor.CloseFailed += (_, args) =>
                                closed.TrySetException(new InvalidOperationException($"TCP listener close failed: {args.ErrorCode}"));

                            socket.Bind(endpoint);
                            try
                            {
                                await run(socket).ConfigureAwait(false);
                            }
                            finally
                            {
                                socket.Unbind(endpoint);
                                var closedOrTimeout = await Task.WhenAny(closed.Task, Task.Delay(_stopTimeout)).ConfigureAwait(false);
                                monitor.Stop();
                                await monitorTask.ConfigureAwait(false);
                                if (closedOrTimeout != closed.Task)
                                    throw new TimeoutException($"TCP listener did not confirm closure of {endpoint}.");
                                await closed.Task.ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    session.RequestStop();
                    Debug.LogError($"NetMQ {name} endpoint on port {port} failed: {exception}");
                }
            });
        }

        // =========================================================
        // 1. ИНИЦИАЛИЗАЦИЯ И ПУБЛИКАЦИЯ ВЫСОКОЧАСТОТНЫХ ДАННЫХ (PUB)
        // =========================================================

        private static async Task PublisherLoop(ServerSession session, PublisherSocket socket)
        {
            lock (_publishLock)
            {
                if (!session.IsRunning)
                    return;
                session.Publisher = socket;
                session.EndpointBound();
            }

            await session.Stopping.ConfigureAwait(false);

            lock (_publishLock)
                session.Publisher = null;
        }


        private static void ReceiverLoop(ServerSession session, SubscriberSocket socket)
        {
            socket.Subscribe(""); // Подписка на все топики
            session.EndpointBound();

            while (session.IsRunning)
            {
                NetMQMessage message = new NetMQMessage();
                if (!socket.TryReceiveMultipartMessage(_socketTimeout, ref message))
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

        /// <summary>
        /// Быстрая публикация любых blittable/value-структур в CDR через NetMQ
        /// </summary>
        public static void PublishCdrTopic<T>(string topicName, in T data) where T : struct
        {
            var session = _session;
            if (session?.IsRunning != true) return;
            if (!UdtpV1Codec.TryGetTypeId(typeof(T), out var typeId))
            {
                // Keep unsupported typed payloads opaque until a stable v1 codec is assigned.
                PublishOpaque(session, topicName, data);
                return;
            }

            byte[] payload = UdtpV1Codec.Encode(data);
            PublishV1(session, topicName, typeId, payload, GetPayloadTimestamp(data));
        }

        /// <summary>
        /// Публикация сырых бинарных массивов (кадры камер, сканы лидара)
        /// </summary>
        public static void PublishRaw(string topicName, byte[] rawData, int typeId = 0)
        {
            var session = _session;
            if (session?.IsRunning != true || rawData == null) return;
            var header = new TopicHeader
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sequence = NextSequence(),
                payloadSize = rawData.Length,
                topicTypeId = typeId
            };
            SendMultipart(session, topicName, header.EncodeV1(), rawData);
        }

        /// <summary>
        /// Публикация legacy raw camera payload с сохранённым ID 2.
        /// </summary>
        public static void PublishCameraRaw(string topicName, byte[] rawData)
        {
            PublishRaw(topicName, rawData, UdtpV1Codec.LegacyCameraTypeId);
        }

        private static void PublishV1(ServerSession session, string topicName, int typeId, byte[] payload, long payloadTimestamp)
        {
            if (payload.Length > int.MaxValue) return;
            var header = new TopicHeader
            {
                timestamp = payloadTimestamp > 0 ? payloadTimestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sequence = NextSequence(),
                payloadSize = payload.Length,
                topicTypeId = typeId
            };
            SendMultipart(session, topicName, header.EncodeV1(), payload);
        }

        private static void PublishOpaque<T>(ServerSession session, string topicName, in T data) where T : struct
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
            SendMultipart(session, topicName, header.EncodeV1(), payload);
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

        private static void SendMultipart(ServerSession session, string topicName, byte[] header, byte[] payload)
        {
            lock (_publishLock)
            {
                if (!session.IsRunning || session.Publisher == null) return;
                var message = new NetMQMessage();
                message.Append(topicName);
                message.Append(header);
                message.Append(payload);
                session.Publisher.TrySendMultipartMessage(TimeSpan.Zero, message);
            }
        }
        // =========================================================
        // 2. СЕРВЕР СЛУЖЕБНЫХ КОМАНД (ROUTER SOCKET)
        // =========================================================
        private static void CommandServerLoop(ServerSession session, RouterSocket socket)
        {
            session.EndpointBound();
            while (session.IsRunning)
            {
                NetMQMessage message = new NetMQMessage();
                if (!socket.TryReceiveMultipartMessage(_socketTimeout, ref message))
                    continue;

                if (message.FrameCount >= 3)
                {
                    var clientIdentity = message[0];
                    byte[] rawBytes = message[2].ToByteArray();
                    string payload = Encoding.UTF8.GetString(rawBytes).Trim();
                    string response = ProcessCommand(payload);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                    socket.TrySendMultipartMessage(_socketTimeout, new NetMQMessage(new[]
                    {
                        clientIdentity,
                        NetMQFrame.Empty,
                        new NetMQFrame(responseBytes)
                    }));
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