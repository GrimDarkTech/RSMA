using RSMA.NetMQ;
using RSMA.uDTP.CDR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RSMA.uDTP
{
    public static class DataBroker
    {
        private static readonly ConcurrentDictionary<string, string> _topicRegistry
            = new ConcurrentDictionary<string, string>();

        private interface ITopicStorage
        {
            object GetValueAsObject();
            void Clear();
        }

        private class TopicStorage<T> : ITopicStorage where T : struct
        {
            public T LatestValue;
            public object GetValueAsObject() => LatestValue;
            public void Clear() => LatestValue = default;
        }

        private static readonly ConcurrentDictionary<string, ITopicStorage> _topics
            = new ConcurrentDictionary<string, ITopicStorage>();

        // Хранилище для входящих из сети сырых байтовых буферов
        private static readonly ConcurrentDictionary<string, byte[]> _rawTopics
            = new ConcurrentDictionary<string, byte[]>();

        // Хранилище сетевых заголовков (если понадобятся timestamp, sequence и т.д.)
        private static readonly ConcurrentDictionary<string, byte[]> _rawHeaders
            = new ConcurrentDictionary<string, byte[]>();

        public static void RegisterTopic<T>(string topicName)
        {
            _topicRegistry.TryAdd(topicName, typeof(T).Name);
        }

        public static void RegisterTopic(string topicName, string typeName)
        {
            _topicRegistry.TryAdd(topicName, typeName);
        }

        public static string GetTopicsJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"topics\":[");

            bool first = true;
            foreach (var kvp in _topicRegistry)
            {
                if (!first) sb.Append(",");
                sb.Append($"{{\"name\":\"{kvp.Key}\",\"type\":\"{kvp.Value}\"}}");
                first = false;
            }

            sb.Append("]}");
            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Publish<T>(string topicName, in T message) where T : struct
        {
            RegisterTopic<T>(topicName);

            if (!_topics.TryGetValue(topicName, out var storage))
            {
                storage = new TopicStorage<T>();
                _topics.TryAdd(topicName, storage);
            }
            ((TopicStorage<T>)storage).LatestValue = message;

            NetMQServer.PublishCdrTopic(topicName, message);
        }

        /// <summary>
        /// Прием топика с байтами данных
        /// </summary>
        public static void UpdateFromNetwork(string topicName, byte[] payload)
        {
            RegisterTopic(topicName, "NetworkPayload");
            _rawTopics[topicName] = payload;
        }

        /// <summary>
        /// Перегрузка: Прием топика с заголовком и байтами данных
        /// </summary>
        public static void UpdateFromNetwork(string topicName, byte[] header, byte[] payload)
        {
            RegisterTopic(topicName, "NetworkPayload");
            _rawHeaders[topicName] = header;
            _rawTopics[topicName] = payload;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetState<T>(string topicName) where T : struct
        {
            // 1. Проверяем сетевой буфер
            if (_rawTopics.TryGetValue(topicName, out var rawBytes))
            {
                if (rawBytes.Length >= Unsafe.SizeOf<T>())
                {
                    return CdrSerializer.Unpack<T>(rawBytes);
                }
            }

            // 2. Локальное хранилище Unity
            if (_topics.TryGetValue(topicName, out var storage))
            {
                return ((TopicStorage<T>)storage).LatestValue;
            }

            return default;
        }

        // =========================================================================
        //  МЕТОДЫ ДЛЯ МОНИТОРА И РЕФЛЕКСИИ (ОБЯЗАТЕЛЬНЫ ДЛЯ RSMADataBrokerMonitor)
        // =========================================================================

        /// <summary>
        /// Возвращает список всех зарегистрированных и активных топиков
        /// </summary>
        public static List<string> GetActiveTopics()
        {
            var activeTopics = new HashSet<string>(_topicRegistry.Keys);

            // Добавляем топики, содержащие сырые данные
            foreach (var key in _rawTopics.Keys)
            {
                activeTopics.Add(key);
            }

            return new List<string>(activeTopics);
        }

        /// <summary>
        /// Извлекает объект состояния топика (для рефлексии в Editor Window)
        /// </summary>
        public static object GetRawState(string topicName)
        {
            // 1. Попытка забрать локальную C#-структуру из _topics
            if (_topics.TryGetValue(topicName, out var storage))
            {
                return storage.GetValueAsObject();
            }

            // 2. Если данных в _topics нет, но есть байты в _rawTopics
            if (_rawTopics.TryGetValue(topicName, out var rawBytes))
            {
                return rawBytes; // Возвращаем байтовый массив (або монитор обработает его размер)
            }

            return null;
        }

        /// <summary>
        /// Метод распаковки байтов в указанный тип Type во время работы Editor (без generic <T>)
        /// </summary>
        public static object GetStateAsType(string topicName, Type targetType)
        {
            if (_rawTopics.TryGetValue(topicName, out var rawBytes))
            {
                int targetSize = Marshal.SizeOf(targetType);
                if (rawBytes.Length >= targetSize)
                {
                    // Распаковываем маршаллингом структуры из byte[]
                    GCHandle handle = GCHandle.Alloc(rawBytes, GCHandleType.Pinned);
                    try
                    {
                        return Marshal.PtrToStructure(handle.AddrOfPinnedObject(), targetType);
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
            }

            if (_topics.TryGetValue(topicName, out var storage))
            {
                return storage.GetValueAsObject();
            }

            return null;
        }
    }
}