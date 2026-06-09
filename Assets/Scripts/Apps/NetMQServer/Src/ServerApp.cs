using RSMA.NetMQ;
using UnityEngine;
using UnityEngine.UI;

namespace RSMA.GUI 
{
    public class ServerApp : Window
    {
        private GameObject _mainPannel = null;

        private Button _stateButton = null;
        private Text _stateText = null;
        private InputField _portInputField = null;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            NetMQServer.Run(5555);
        }

        void Update()
        {
            NetMQServer.Update();
        }

        void OnApplicationQuit()
        {
            NetMQServer.Stop();
        }

        public void SwitchState() 
        {
            if (NetMQServer.IsRunning) 
            {
                NetMQServer.Stop();
                Debug.Log("Stopping NetMQServer");
                _stateText.text = "Offline";
                _stateText.color = Color.red;
                return;
            }
            int port = 5555;
            
            if (!int.TryParse(_portInputField.text, out port)) 
            {
                _portInputField.text = "5555";
                port = 5555;
            }

            NetMQServer.Run(port);
            Debug.Log($"Running NetMQServer on {port}");
            _stateText.text = "Online";
            _stateText.color = Color.green;
        }

        protected override void Start() 
        { 
            base.Start();

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

            Close();
        }

        public override void Close()
        {
            base.Close();
            _mainPannel.SetActive(false);
        }

        public override void Open()
        {
            base.Open();
            _mainPannel.SetActive(true);
        }
    }
}

