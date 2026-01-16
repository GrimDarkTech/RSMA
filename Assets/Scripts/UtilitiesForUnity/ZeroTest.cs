using System;
using System.Collections.Generic;
using System.Text;
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;

public class ZmqServer
{
    private RouterSocket _server;
    private NetMQPoller _poller;
    private readonly int _port = 7777;
    public bool IsRunning { get; private set; }

    public event Action<string, string> OnMessageReceived;

    public void Start()
    {
        if (IsRunning) 
        {
            return;
        } 

        _server = new RouterSocket();
        _server.Bind($"tcp://*:{_port}");

        _server.ReceiveReady += (s, e) =>
        {
            // ZeroMQ Router получает сообщения в формате:
            // 1-й фрейм: Идентификатор клиента (Identity)
            // 2-й фрейм: Пустой фрейм (разделитель)
            // 3-й фрейм: Само сообщение
            var clientAddress = e.Socket.ReceiveFrameBytes();
            e.Socket.SkipFrame(); // Пропускаем пустой фрейм
            string message = e.Socket.ReceiveFrameString();

            string clientId = Encoding.UTF8.GetString(clientAddress);

            // Вызываем событие (не забывайте про Thread Safety в Unity!)
            OnMessageReceived?.Invoke(clientId, message);
        };

        _poller = new NetMQPoller { _server };
        _poller.RunAsync(); // Запуск в отдельном потоке

        IsRunning = true;
        Debug.Log("ZMQ Server started on port " + _port);
    }

    public void SendCommand(string clientId, string command)
    {
        if (!IsRunning) return;

        // Отправка клиенту: [ID] -> [Empty] -> [Data]
        _server.SendMoreFrame(clientId)
               .SendMoreFrameEmpty()
               .SendFrame(command);
    }

    public void Stop()
    {
        _poller?.Stop();
        _server?.Dispose();
        NetMQConfig.Cleanup(); // Важно для корректного закрытия потоков
        IsRunning = false;
    }
}