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

                _labelObject = new GameObject();
                _labelObject.name = "Label " + label;
                RectTransform labelTransform = _labelObject.AddComponent<RectTransform>();

                labelTransform.SetParent(_transform);

                _labelBackground = _labelObject.AddComponent<Image>();
                _labelBackground.color = new Color(0.15f, 0.15f, 0.15f);

                labelTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                labelTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
                labelTransform.anchorMin = new Vector2(0.5f, 0.5f);
                labelTransform.anchorMax = new Vector2(0.5f, 0.5f);
                labelTransform.pivot = new Vector2(0.5f, 0.5f);
                labelTransform.anchoredPosition = new Vector3(0, height / 2, 0);


                _labelTextObject = new GameObject();
                _labelTextObject.name = "Text";
                RectTransform labelTextTransform = _labelTextObject.AddComponent<RectTransform>();
                labelTextTransform.SetParent(labelTransform);
                _labelText = _labelTextObject.AddComponent<Text>();
                labelTextTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                labelTextTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
                labelTextTransform.anchoredPosition = new Vector3(0, 0, 0);
                _labelText.text = "    [" + icon + "]    " + label;
                _labelText.font = font;
                _labelText.fontSize = 20;
                _labelText.alignment = TextAnchor.MiddleLeft;

                if (closeButton != null) 
                {
                    _closeButton = Instantiate<GameObject>(closeButton);
                    RectTransform buttonTransform = _closeButton.GetComponent<RectTransform>();
                    buttonTransform.SetParent(labelTransform);
                    buttonTransform.anchoredPosition = new Vector3(width / 2 - 60, 0, 0);

                    Button button = _closeButton.GetComponent<Button>();
                    button.onClick.AddListener(Close);
                }

            }
        }

        public virtual void Open() 
        {
            _background.enabled = true;
            _labelObject.SetActive(true);
            _labelTextObject.SetActive(true);
            isEnabled = true;
        }

        public virtual void Close() 
        {
            _background.enabled = false;
            _labelObject.SetActive(false);
            _labelTextObject.SetActive(false);
            isEnabled = false;
        }

        public void OnDrag(PointerEventData data) 
        { 
            _transform.anchoredPosition += data.delta;
        }
    }
}