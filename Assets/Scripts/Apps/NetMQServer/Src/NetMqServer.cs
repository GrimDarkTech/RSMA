using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;

namespace RSMA.NetMQ
{
    [UnityEditor.InitializeOnLoad]
    public static class NetMQServer
    {
        private static bool _isRunning;
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();

        public static bool IsRunning => _isRunning;

        static NetMQServer()
        {
            
            UnityEditor.EditorApplication.playModeStateChanged += (state) => 
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) 
                {
                    Stop();
                }

            };
        }

        public static void Run(int serverPort = 5555)
        {
            if (_isRunning) return;
            _isRunning = true;

            // Запуск сервера в отдельном потоке
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
            using (var server = new RouterSocket()) // Меняем на Router
            {
                server.Bind($"tcp://*:{serverPort}");

                while (_isRunning)
                {
                    // Router получает сообщение в формате: [Identity, EmptyFrame, Data]
                    var message = server.ReceiveMultipartMessage();

                    if (message.FrameCount >= 3)
                    {
                        var clientIdentity = message[0]; // ID клиента
                        var payload = message[2].ConvertToString(); // Сами данные

                        string response = ProcessCommand(payload);

                        // Отправляем ответ обратно тому же клиенту
                        server.SendMultipartMessage(new NetMQMessage(new[] { clientIdentity, NetMQFrame.Empty, new NetMQFrame(response) }));
                    }
                }
            }
            NetMQConfig.Cleanup();
        }

        private static string ProcessCommand(string command)
        {
            // Пример: если пришло "PrintMessage:hello"
            if (command.StartsWith("PrintMessage:"))
            {
                string msg = command.Split(':')[1];
                EnqueueAction(() => Debug.Log($"Сообщение из Python: {msg}"));
                return "OK: Message printed";
            }
            return "Error: Unknown command";
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
    }
}