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

            float headerHeight = 40f;
            float inputHeight = 40f;
            float padding = 10f;

            // --- 1. ПОЛЕ ВВОДА (Bottom-Stretch) ---
            _inputfieldObject = new GameObject("Terminal InputField");
            _inputfieldObject.layer = 5;

            RectTransform inputfieldTransform = _inputfieldObject.AddComponent<RectTransform>();
            inputfieldTransform.SetParent(_transform, false);

            // Привязка к нижнему краю, растягивание по ширине
            inputfieldTransform.anchorMin = new Vector2(0f, 0f);
            inputfieldTransform.anchorMax = new Vector2(1f, 0f);
            inputfieldTransform.pivot = new Vector2(0.5f, 0f);

            // Отступы: padding слева/справа, padding снизу
            inputfieldTransform.anchoredPosition = new Vector2(0f, padding);
            inputfieldTransform.sizeDelta = new Vector2(-padding * 2, inputHeight);

            Image inputfieldBackground = _inputfieldObject.AddComponent<Image>();
            inputfieldBackground.color = new Color(0.12f, 0.12f, 0.12f);

            // Текст внутри InputField (Растягиваем на всё поле ввода)
            GameObject inputfieldTextObject = new GameObject("Text");
            inputfieldTextObject.layer = 5;
            RectTransform inputfieldTextTransform = inputfieldTextObject.AddComponent<RectTransform>();
            inputfieldTextTransform.SetParent(inputfieldTransform, false);
            inputfieldTextTransform.anchorMin = Vector2.zero;
            inputfieldTextTransform.anchorMax = Vector2.one;
            inputfieldTextTransform.offsetMin = new Vector2(10f, 0f);
            inputfieldTextTransform.offsetMax = new Vector2(-10f, 0f);

            Text inputFieldText = inputfieldTextObject.AddComponent<Text>();
            inputFieldText.font = terminalFont;
            inputFieldText.fontSize = 16;
            inputFieldText.alignment = TextAnchor.MiddleLeft;

            _inputfield = _inputfieldObject.AddComponent<InputField>();
            _inputfield.textComponent = inputFieldText;
            _inputfield.targetGraphic = inputfieldBackground;
            _inputfield.onEndEdit.AddListener(OnEndEdit);


            // --- 2. ОБЛАСТЬ ТЕКСТА ТЕРМИНАЛА (Full-Stretch между заголовком и полем ввода) ---
            _textObject = new GameObject("Terminal Text");
            _textObject.layer = 5;

            RectTransform textTransform = _textObject.AddComponent<RectTransform>();
            textTransform.SetParent(_transform, false);

            // Растягиваем объект на всю свободную область окна
            textTransform.anchorMin = Vector2.zero;
            textTransform.anchorMax = Vector2.one;

            // Задаем отступы (Left, Bottom, Right, Top)
            // Bottom = высота поля ввода + отступы; Top = высота заголовка + отступ
            textTransform.offsetMin = new Vector2(padding, inputHeight + padding * 2);
            textTransform.offsetMax = new Vector2(-padding, -headerHeight - padding);

            _text = _textObject.AddComponent<Text>();
            _text.font = terminalFont;
            _text.fontSize = 16;
            _text.alignment = TextAnchor.UpperLeft;
            _text.text = "RSMA Terminal..\n";
            _text.verticalOverflow = VerticalWrapMode.Overflow;

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

