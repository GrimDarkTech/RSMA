using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

public class RSMANetMQServer
{
    private RouterSocket serverSocket;
    private Thread receiveThread;
    private CancellationTokenSource cancellationTokenSource;
    private ConcurrentDictionary<string, byte[]> clientIdentities;   // clientName → identity
    private ConcurrentQueue<OutgoingMessage> outgoingQueue;          // потокобезопасная очередь исходящих
    private bool isRunning;

    // Адрес для привязки (по умолчанию tcp://*:5555)
    public string BindAddress { get; set; } = "tcp://*:5555";

    public RSMANetMQServer()
    {
        clientIdentities = new ConcurrentDictionary<string, byte[]>();
        outgoingQueue = new ConcurrentQueue<OutgoingMessage>();
    }

    // Вызывается из внешнего кода (например, из основного потока Unity) для отправки сообщения конкретному клиенту
    public void SendMessageToClientAsync(string clientName, string message)
    {
        if (!string.IsNullOrEmpty(clientName) && !string.IsNullOrEmpty(message))
        {
            outgoingQueue.Enqueue(new OutgoingMessage(clientName, message));
        }
    }

    // Запуск сервера
    public void Start()
    {
        if (isRunning) return;

        // Для совместимости с Unity может потребоваться вызов AsyncIO.ForceDotNet.Force()
        AsyncIO.ForceDotNet.Force(); // опционально

        cancellationTokenSource = new CancellationTokenSource();
        clientIdentities.Clear();
        outgoingQueue.Clear();

        serverSocket = new RouterSocket();
        serverSocket.Bind(BindAddress);

        isRunning = true;
        receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();

        // // CommandHandler.terminal?.Print($"NetMQ server started on {BindAddress}");
    }

    // Остановка сервера и освобождение ресурсов
    public void Stop()
    {
        if (!isRunning) return;

        cancellationTokenSource.Cancel();
        serverSocket?.Close();
        receiveThread?.Join(1000); // ждём завершения потока не более 1 секунды

        serverSocket = null;
        isRunning = false;

        // При необходимости можно вызвать NetMQConfig.Cleanup(), если других сокетов не осталось
        // // CommandHandler.terminal?.Print("NetMQ server stopped");
    }

    // Основной цикл приёма сообщений – выполняется в отдельном потоке
    private void ReceiveLoop()
    {
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                // Блокируемся до получения сообщения (или закрытия сокета)
                NetMQMessage incoming = serverSocket.ReceiveMultipartMessage();

                // Сообщение от ROUTER содержит минимум 2 фрейма: [identity, ...]
                if (incoming.FrameCount < 2)
                    continue;

                byte[] identity = incoming[0].ToByteArray();   // идентификатор клиента
                string clientName = Convert.ToBase64String(identity); // превращаем в читаемое имя

                // Если клиент новый – вызываем OnClientConnected
                if (!clientIdentities.ContainsKey(clientName))
                {
                    clientIdentities[clientName] = identity;
                    OnClientConnected(clientName);
                }

                // Тело сообщения (предполагаем, что это UTF-8 строка)
                string message = incoming[1].ConvertToString(Encoding.UTF8);

                // Обрабатываем сообщение (внутри может вызываться SendMessageToClientAsync)
                OnMessageReceived(clientName, message);

                // После обработки отправляем все накопившиеся исходящие сообщения
                DrainOutgoingQueue();
            }
            catch (TerminatingException)
            {
                // Сокет закрывается – выходим из цикла
                break;
            }
            catch (Exception ex)
            {
                //Debug.lo.Print($"Ошибка в цикле приёма: {ex.Message}");
            }
        }
    }

    // Отправляет все ожидающие сообщения из очереди
    private void DrainOutgoingQueue()
    {
        while (outgoingQueue.TryDequeue(out OutgoingMessage outgoing))
        {
            if (clientIdentities.TryGetValue(outgoing.ClientName, out byte[] identity))
            {
                try
                {
                    // Формируем многочастное сообщение: [identity, данные]
                    serverSocket.SendMoreFrame(identity).SendFrame(Encoding.UTF8.GetBytes(outgoing.Message));
                }
                catch (Exception ex)
                {
                    // // CommandHandler.terminal?.Print($"Не удалось отправить клиенту {outgoing.ClientName}: {ex.Message}");
                }
            }
            else
            {
                // CommandHandler.terminal?.Print($"Неизвестный клиент: {outgoing.ClientName}");
            }
        }
    }

    // Вызывается при обнаружении нового клиента (первое сообщение)
    public void OnClientConnected(string clientName)
    {
        // // CommandHandler.terminal?.Print($"\nКлиент {clientName} подключился к серверу");
    }

    // Не реализовано – NetMQ не предоставляет автоматических уведомлений об отключении.
    // Можно добавить мониторинг или heartbeat'ы, если требуется.
    public void OnClientDisconnected(string clientName)
    {
        //Debug.Lo($"\nКлиент {clientName} отключился от сервера");
    }

    // Вызывается при получении сообщения от клиента
    public void OnMessageReceived(string clientName, string message)
    {
        //if (// // CommandHandler.terminal == null) return;

        if (message.Contains("<|CMD|>"))
        {
            string cmd = message.Replace("<|CMD|>", "");
            string reply = CommandHandler.Execute(cmd);
            SendMessageToClientAsync(clientName, reply);
        }
        else
        {
            // // CommandHandler.terminal.Print($"\nПолучено сообщение от {clientName}: {message}");
        }
    }

    // Вспомогательная структура для очереди исходящих сообщений
    private struct OutgoingMessage
    {
        public string ClientName { get; }
        public string Message { get; }

        public OutgoingMessage(string clientName, string message)
        {
            ClientName = clientName;
            Message = message;
        }
    }
}