
namespace RSMA.NetMQ
{
    public class NetworkPacket
    {
        public string Action { get; set; }     // "publish" или "get"
        public string TopicName { get; set; }
        public string TopicType { get; set; }
        public string Data { get; set; }       // JSON
    }
}