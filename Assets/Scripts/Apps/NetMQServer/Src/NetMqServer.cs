using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;

namespace RSMA.NetMQ
{
    public static class NetMQServer
    {
        private static bool _isRunning;
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();

        public static bool IsRunning => _isRunning;

        public static void Run()
        {
            if (_isRunning) return;
            _isRunning = true;

            // Запуск сервера в отдельном потоке
            Task.Run(() => ServerLoop());
        }

        public static void Stop()
        {
            _isRunning = false;
        }

        private static void ServerLoop()
        {
            AsyncIO.ForceDotNet.Force(); // Обязательно для NetMQ в Unity
            using (var server = new ResponseSocket())
            {
                server.Bind("tcp://*:5555");

                while (_isRunning)
                {
                    string message;
                    if (server.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out message))
                    {
                        Debug.Log($"Получено: {message}");

                        // Парсинг команды и добавление в очередь Unity
                        string response = ProcessCommand(message);

                        server.SendFrame(response);
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