using UnityEngine;
using RSMA.NetMQ;

namespace RSMA.GUI 
{
    public class ServerApp : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            NetMQServer.Run();
        }

        void Update()
        {
            NetMQServer.Update();
        }

        void OnApplicationQuit()
        {
            NetMQServer.Stop();
        }
    }
}

