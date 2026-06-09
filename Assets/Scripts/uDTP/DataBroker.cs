using System;
using System.Collections.Generic;

namespace RSMA.uDTP 
{
    public static class DataBroker
    {
        // Ключ: (Тип данных, Имя топика)
        private static readonly Dictionary<(Type, string), object> _latestStates = new Dictionary<(Type, string), object>();
        private static readonly object _lock = new object();

        // Публикация в конкретный топик
        public static void Publish<T>(string topicName, T message)
        {
            lock (_lock)
            {
                _latestStates[(typeof(T), topicName)] = message;
            }
        }

        // Получение состояния конкретного топика
        public static T GetState<T>(string topicName)
        {
            lock (_lock)
            {
                var key = (typeof(T), topicName);
                if (_latestStates.TryGetValue(key, out var state))
                {
                    return (T)state;
                }
                return default(T);
            }
        }
    }
}