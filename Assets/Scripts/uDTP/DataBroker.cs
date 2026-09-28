using RSMA.NetMQ;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace RSMA.uDTP
{
    public static class DataBroker
    {
        private static readonly ConcurrentDictionary<string, string> _topicRegistry
            = new ConcurrentDictionary<string, string>();
        private interface ITopicStorage
        {
            void Clear();
        }

        private class TopicStorage<T> : ITopicStorage where T : struct
        {
            public T LatestValue;
            public void Clear() => LatestValue = default;
        }

        private static readonly ConcurrentDictionary<string, ITopicStorage> _topics
            = new ConcurrentDictionary<string, ITopicStorage>();

        public static void RegisterTopic<T>(string topicName)
        {
            _topicRegistry.TryAdd(topicName, typeof(T).Name);
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

            // Ссылочные типы (кадр камеры) отправляются как Raw, а структура — как CDR
            NetMQServer.PublishCdrTopic(topicName, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetState<T>(string topicName) where T : struct
        {
            if (_topics.TryGetValue(topicName, out var storage))
            {
                return ((TopicStorage<T>)storage).LatestValue;
            }
            return default;
        }
    }
}