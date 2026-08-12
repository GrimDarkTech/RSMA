using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RSMA.GUI 
{
    public class Window : MonoBehaviour, IDragHandler
    {
        public float width = 800f;
        public float height = 600f;
        public Color color = Color.gray;

        public float positionX = 0;
        public float positionY = 0;

        public Canvas masterCanvas = null;
        public string label = "Window";
        public string icon = "C#";
        public Font font = null;
        public GameObject closeButton = null;

        protected RectTransform _transform = null;
        protected bool isEnabled = true;

        private Image _background = null;

        private GameObject _labelObject = null;
        private GameObject _labelTextObject = null;
        private Image _labelBackground = null;
        private Text _labelText = null;
        private GameObject _closeButton = null;

        protected virtual void Start()
        {
            if (masterCanvas != null)
            {
                _background = gameObject.AddComponent<Image>();
                _background.color = color;

                _transform = gameObject.GetComponent<RectTransform>();
                _transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                _transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

                // --- СОЗДАНИЕ ЗАГОЛОВКА ---
                _labelObject = new GameObject("Label " + label);
                _labelObject.layer = 5;

                RectTransform labelTransform = _labelObject.AddComponent<RectTransform>();
                labelTransform.SetParent(_transform, false);

                _labelBackground = _labelObject.AddComponent<Image>();
                _labelBackground.color = new Color(0.15f, 0.15f, 0.15f);

                // ЯКОРЬ: Привязываем к ВЕРХНЕМУ краю и растягиваем по ширине (Top-Stretch)
                labelTransform.anchorMin = new Vector2(0f, 1f);
                labelTransform.anchorMax = new Vector2(1f, 1f);
                labelTransform.pivot = new Vector2(0.5f, 1f); // Точка привязки — верхний центр

                labelTransform.anchoredPosition = Vector2.zero; // Вплотную к верхнему краю
                labelTransform.sizeDelta = new Vector2(0, 40f);  // Высота заголовка 40px, ширина адаптивная (0 = 100% ширины)

                // --- ТЕКСТ ЗАГОЛОВКА ---
                _labelTextObject = new GameObject("Text");
                _labelTextObject.layer = 5;

                RectTransform labelTextTransform = _labelTextObject.AddComponent<RectTransform>();
                labelTextTransform.SetParent(labelTransform, false);

                // Растягиваем текст по всей ширине заголовка с небольшим отступом слева
                labelTextTransform.anchorMin = Vector2.zero;
                labelTextTransform.anchorMax = Vector2.one;
                labelTextTransform.offsetMin = new Vector2(15f, 0f);
                labelTextTransform.offsetMax = new Vector2(-50f, 0f);

                _labelText = _labelTextObject.AddComponent<Text>();
                _labelText.text = $"[{icon}]  {label}";
                _labelText.font = font;
                _labelText.fontSize = 18;
                _labelText.alignment = TextAnchor.MiddleLeft;

                // --- КНОПКА ЗАКРЫТИЯ ---
                if (closeButton != null)
                {
                    _closeButton = Instantiate(closeButton);
                    _closeButton.layer = 5;

                    RectTransform buttonTransform = _closeButton.GetComponent<RectTransform>();
                    buttonTransform.SetParent(labelTransform, false);

                    // ЯКОРЬ: Привязываем к правому верхнему углу заголовка
                    buttonTransform.anchorMin = new Vector2(1f, 0.5f);
                    buttonTransform.anchorMax = new Vector2(1f, 0.5f);
                    buttonTransform.pivot = new Vector2(1f, 0.5f);

                    buttonTransform.anchoredPosition = new Vector2(-10f, 0f); // 10px от правого края
                    buttonTransform.sizeDelta = new Vector2(30f, 30f);        // Фиксированный размер кнопки

                    Button button = _closeButton.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.AddListener(Close);
                    }
                }
            }
        }

        public virtual void Open() 
        {
            _background.enabled = true;
            _labelObject.SetActive(true);
            _labelTextObject.SetActive(true);
            _closeButton.SetActive(true);
            isEnabled = true;
        }

        public virtual void Close() 
        {
            _background.enabled = false;
            _labelObject.SetActive(false);
            _labelTextObject.SetActive(false);
            _closeButton.SetActive(false);
            isEnabled = false;
        }

        public void OnDrag(PointerEventData data) 
        { 
            _transform.anchoredPosition += data.delta;
        }

        public bool isOpened() 
        {
            return isEnabled;
        }
    }
}