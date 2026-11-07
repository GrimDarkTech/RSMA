using RSMA.NetMQ;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace RSMA.GUI 
{
    public class ServerApp : Window
    {
        private static ServerApp _instance;
        private static EventSystem _eventSystem;

        private GameObject _mainPannel = null;

        private Button _stateButton = null;
        private Text _stateText = null;
        private InputField _portInputField = null;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Duplicate RSMA-GUI instance detected; removing the duplicate prefab root.", this);
                Destroy(GetGuiRoot().gameObject);
                return;
            }

            _instance = this;
            // ServerApp lives under RSMA-GUI/Canvas/Apps, so it is not a scene root.
            // Persist the prefab root, not this child window.
            DontDestroyOnLoad(GetGuiRoot().gameObject);
            EnsureEventSystem();
            SceneManager.sceneLoaded -= RemoveDuplicateEventSystems;
            SceneManager.sceneLoaded += RemoveDuplicateEventSystems;
            NetMQServer.Run(5555);
        }

        private static void EnsureEventSystem()
        {
            if (_eventSystem == null)
                _eventSystem = FindObjectOfType<EventSystem>();

            if (_eventSystem != null)
            {
                DontDestroyOnLoad(_eventSystem.gameObject);
                return;
            }

            var eventSystemObject = new GameObject("RSMA EventSystem");
            _eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventSystemObject);
        }

        private static void RemoveDuplicateEventSystems(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
            foreach (var candidate in FindObjectsOfType<EventSystem>())
            {
                if (candidate != _eventSystem)
                    Destroy(candidate.gameObject);
            }
        }

        private Transform GetGuiRoot()
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (current.name == "RSMA-GUI")
                    return current;

                var appsCanvas = current.Find("Apps");
                var controlCanvas = current.Find("Control-Panel");
                if (appsCanvas != null && appsCanvas.GetComponent<Canvas>() != null &&
                    controlCanvas != null && controlCanvas.GetComponent<Canvas>() != null)
                    return current;
            }

            return transform.root;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            _instance = null;
            _eventSystem = null;
        }

        void Update()
        {
            if (_instance != this)
                return;
            NetMQServer.Update();
        }

        void OnApplicationQuit()
        {
            if (_instance == this)
                NetMQServer.Stop();
        }

        private void OnDestroy()
        {
            if (_instance != this)
                return;
            _instance = null;
            NetMQServer.Stop();
        }

        public void SwitchState() 
        {
            if (NetMQServer.IsRunning) 
            {
                NetMQServer.Stop();
                Debug.Log("Stopping NetMQServer");
                SetServerState("Offline", Color.red);
                return;
            }
            int port = 5555;
            
            if (_portInputField == null || !int.TryParse(_portInputField.text, out port) || port < 1 || port > 65535)
            {
                if (_portInputField != null)
                    _portInputField.text = "5555";
                port = 5555;
            }

            NetMQServer.Run(port);
            Debug.Log($"Running NetMQServer on {port}");
            SetServerState(NetMQServer.IsRunning ? "Online" : "Offline",
                NetMQServer.IsRunning ? Color.green : Color.red);
        }

        protected override void Start() 
        { 
            if (_instance != this)
                return;

            base.Start();
            if (!IsInitialized)
                return;

            _mainPannel = UIBuilder.CreatePanel("GridContainer", _transform);
            RectTransform rt = _mainPannel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var portLabel = UIBuilder.CreateLabel("Port label", _mainPannel.transform, "Port: ", font, 25);
            UIBuilder.PlaceInGrid(portLabel.gameObject, 0, 0, 1, 1, 3, 2);
            _portInputField = UIBuilder.CreateInputField("Port input", _mainPannel.transform, font, 25, "5555");
            UIBuilder.PlaceInGrid(_portInputField.gameObject, 0, 1, 1, 1, 3, 2);
            _portInputField.text = "5555";

            var stateLabel = UIBuilder.CreateLabel("State label", _mainPannel.transform, "Server state:", font, 25);
            UIBuilder.PlaceInGrid(stateLabel.gameObject, 1, 0, 1, 1, 3, 2);
            _stateText = UIBuilder.CreateLabel("State text", _mainPannel.transform, "Online", font, 25);
            _stateText.color = Color.green;
            UIBuilder.PlaceInGrid(_stateText.gameObject, 1, 1, 1, 1, 3, 2);

            _stateButton = UIBuilder.CreateButton("State button", _mainPannel.transform, "Enable/Disable", font, 25, SwitchState);
            UIBuilder.PlaceInGrid(_stateButton.gameObject, 2, 0, 1, 2, 3, 2);

            SetServerState(NetMQServer.IsRunning ? "Online" : "Offline",
                NetMQServer.IsRunning ? Color.green : Color.red);
            Close();
        }

        public override void Close()
        {
            base.Close();
            if (_mainPannel != null)
                _mainPannel.SetActive(false);
        }

        public override void Open()
        {
            base.Open();
            if (_mainPannel != null)
                _mainPannel.SetActive(true);
        }

        private void SetServerState(string text, Color color)
        {
            if (_stateText == null)
                return;
            _stateText.text = text;
            _stateText.color = color;
        }
    }
}

