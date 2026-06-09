using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.UI;

namespace RSMA.GUI 
{
    public class Terminal : Window
    {
        public Font terminalFont = null;
        private List<string> commandHistory = new List<string>();
        private Text _text = null;
        private GameObject _textObject = null;
        private GameObject _inputfieldObject = null;
        private InputField _inputfield = null;
        private int _commandIndex = 0;
        private int _lineIndex = 0;

        protected override void Start()
        {
            base.Start();
            LoadCommandHistory();

            _textObject = new GameObject();
            _textObject.name = "Terminal Text";
            RectTransform textTransform = _textObject.AddComponent<RectTransform>();
            textTransform.SetParent(_transform);
            textTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 100);
            textTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 40);
            textTransform.anchoredPosition = new Vector3(0, 10, 0);

            _text = _textObject.AddComponent<Text>();
            _text.font = terminalFont;
            _text.fontSize = 18;
            _text.alignment = TextAnchor.UpperLeft;
            _text.text = "RSMA Terminal..\n";
            _text.verticalOverflow = VerticalWrapMode.Overflow;

            _inputfieldObject = new GameObject();
            _inputfieldObject.name = "Terminal InputField";
            RectTransform inputfieldTransform = _inputfieldObject.AddComponent<RectTransform>();
            inputfieldTransform.SetParent(_transform);
            inputfieldTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
            inputfieldTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 40);
            inputfieldTransform.anchoredPosition = new Vector3(0, -(height / 2) + 30, 0);

            GameObject inputfieldPlaceholderObject = new GameObject();
            inputfieldPlaceholderObject.name = "Placeholder";
            RectTransform placeholderTransform = inputfieldPlaceholderObject.AddComponent<RectTransform>();
            placeholderTransform.parent = inputfieldTransform;
            placeholderTransform.anchoredPosition = new Vector3(0, 0, 0);
            placeholderTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
            placeholderTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 40);
            Text placeholderText = inputfieldPlaceholderObject.AddComponent<Text>();
            placeholderText.text = "Enter the commnad";
            placeholderText.font = terminalFont;
            placeholderText.fontSize = 18;
            placeholderText.alignment = TextAnchor.MiddleLeft;

            GameObject inputfieldTextObject = new GameObject();
            inputfieldTextObject.name = "Text";
            RectTransform inputfieldTextTransform = inputfieldTextObject.AddComponent<RectTransform>();
            inputfieldTextTransform.parent = inputfieldTransform;
            inputfieldTextTransform.anchoredPosition = new Vector3(0, 0, 0);
            inputfieldTextTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
            inputfieldTextTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 40);
            Text inputFieldText = inputfieldTextObject.AddComponent<Text>();
            inputFieldText.font = terminalFont;
            inputFieldText.fontSize = 18;
            inputFieldText.alignment = TextAnchor.MiddleLeft;

            Image inputfieldBackground = _inputfieldObject.AddComponent<Image>();
            inputfieldBackground.color = new Color(0.12f, 0.12f, 0.12f);

            _inputfield = _inputfieldObject.AddComponent<InputField>();
            _inputfield.placeholder = placeholderText;
            _inputfield.textComponent = inputFieldText;
            _inputfield.targetGraphic = inputfieldBackground;

            _inputfield.onEndEdit.AddListener(OnEndEdit);

            Close();
        }

        private void OnEndEdit(string message) 
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                _lineIndex++;
                if (_lineIndex > 13) 
                {
                    _lineIndex = 0;
                    _text.text = "";
                }
                string result = CommandHandler.Execute(message);

                _text.text += ">> " + message + "\n";
                _text.text += result + "\n";
                _inputfield.text = "";

                _inputfield.ActivateInputField();
                commandHistory.Add(message);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) && _commandIndex > 0)
            {
                _commandIndex--;
                _inputfield.text = commandHistory[_commandIndex];
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) && (_commandIndex < commandHistory.Count - 1))
            {
                _commandIndex++;
                _inputfield.text = commandHistory[_commandIndex];
            }
        }

        private void LoadCommandHistory() 
        {
            string filepath = "./RSMA/Apps/Terminal/Cache.lru";

            if(File.Exists(filepath)) 
            {
                string json = File.ReadAllText(filepath);

                commandHistory = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
        }

        private void SaveCommandHistory()
        {
            string file = "Cache.lru";
            string directory = "./RSMA/Apps/Terminal/";

            string json = JsonConvert.SerializeObject(commandHistory, Formatting.Indented);

            if (!Directory.Exists(directory)) 
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(directory + file, json);
        }

        public override void Close()
        {
            base.Close();
            _inputfieldObject.SetActive(false);
            _textObject.SetActive(false);
            SaveCommandHistory();
        }

        public override void Open()
        {
            base.Open();
            _inputfieldObject.SetActive(true);
            _textObject.SetActive(true);
        }
    }
}

