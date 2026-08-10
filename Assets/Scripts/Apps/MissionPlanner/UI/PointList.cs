using RSMA.GUI;
using RSMA.MissionPlanner.Core;
using RSMA.uDTP.Topics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RSMA.MissionPlanner.UI
{
    public class PointList
    {
        private Transform _parent;
        private Font _font;
        private GameObject _panel;
        private ScrollRect _scrollRect;
        private GameObject _content;
        private List<GameObject> _pointItems = new List<GameObject>();
        private Color _selectedColor = new Color(0.2f, 0.6f, 1f, 1f);
        private Color _defaultColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        private Color _hoverColor = new Color(0.7f, 0.8f, 1f, 1f);
        private int _selectedIndex = -1;

        public PointList(Transform parent, Font font)
        {
            _parent = parent;
            _font = font;
            CreatePanel();
            SubscribeToEvents();
        }

        private void CreatePanel()
        {
            _panel = UIBuilder.CreatePanel("PointList", _parent);
            RectTransform rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(1, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Настройка фона
            var bgImage = _panel.GetComponent<Image>();
            if (bgImage == null)
                bgImage = _panel.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Создаем ScrollRect
            _scrollRect = _panel.AddComponent<ScrollRect>();

            // Создаем Viewport
            var viewport = UIBuilder.CreatePanel("Viewport", _panel.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(5, 5);
            viewportRt.offsetMax = new Vector2(-5, -5);

            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Создаем Content
            _content = UIBuilder.CreatePanel("Content", viewport.transform);
            var contentRt = _content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            // Настраиваем ScrollRect
            _scrollRect.viewport = viewportRt;
            _scrollRect.content = contentRt;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 20f;

            // Добавляем скроллбар
            CreateScrollbar();
        }

        private void CreateScrollbar()
        {
            var scrollbarObj = UIBuilder.CreatePanel("Scrollbar", _panel.transform);
            var scrollbarRt = scrollbarObj.GetComponent<RectTransform>();
            scrollbarRt.anchorMin = new Vector2(1, 0);
            scrollbarRt.anchorMax = new Vector2(1, 1);
            scrollbarRt.offsetMin = new Vector2(-20, 5);
            scrollbarRt.offsetMax = new Vector2(-5, -5);

            var scrollbar = scrollbarObj.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.TopToBottom;

            // Создаем Handle
            var handle = UIBuilder.CreatePanel("Handle", scrollbarObj.transform);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0, 0);
            handleRt.anchorMax = new Vector2(1, 0.3f);
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);

            // Создаем трек
            var track = UIBuilder.CreatePanel("Track", scrollbarObj.transform);
            var trackRt = track.GetComponent<RectTransform>();
            trackRt.anchorMin = Vector2.zero;
            trackRt.anchorMax = Vector2.one;
            trackRt.offsetMin = Vector2.zero;
            trackRt.offsetMax = Vector2.zero;

            var trackImage = track.AddComponent<Image>();
            trackImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImage;
            _scrollRect.verticalScrollbar = scrollbar;
        }

        private GameObject CreatePointItem(int index, TrajectoryPoint point)
        {
            var item = UIBuilder.CreatePanel($"PointItem_{index}", _content.transform);

            var layoutElement = item.AddComponent<LayoutElement>();
            layoutElement.minHeight = 40;
            layoutElement.preferredHeight = 40;

            // Устанавливаем размеры
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0, 1);
            itemRt.anchorMax = new Vector2(1, 1);
            itemRt.pivot = new Vector2(0.5f, 1);
            itemRt.sizeDelta = new Vector2(0, 40);

            // Фон элемента
            var bg = item.AddComponent<Image>();
            bg.color = _defaultColor;

            // Добавляем кнопку для клика
            var button = item.AddComponent<Button>();
            var colors = new ColorBlock
            {
                normalColor = _defaultColor,
                highlightedColor = _hoverColor,
                pressedColor = new Color(0.4f, 0.4f, 0.4f, 1f),
                selectedColor = _selectedColor,
                disabledColor = Color.gray,
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
            button.colors = colors;

            int capturedIndex = index;
            button.onClick.AddListener(() => OnItemClick(capturedIndex));

            // Добавляем контекстное меню (правый клик)
            var trigger = item.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerClick
            };
            entry.callback.AddListener((data) =>
            {
                var pointerData = (PointerEventData)data;
                if (pointerData.button == PointerEventData.InputButton.Right)
                {
                    ShowContextMenu(capturedIndex);
                }
            });
            trigger.triggers.Add(entry);

            // Индекс точки
            var indexLabel = UIBuilder.CreateLabel("IndexLabel", item.transform, $"{index + 1}.", _font, 18);
            var indexRt = indexLabel.GetComponent<RectTransform>();
            indexRt.anchorMin = new Vector2(0, 0);
            indexRt.anchorMax = new Vector2(0.1f, 1);
            indexRt.offsetMin = new Vector2(10, 0);
            indexRt.offsetMax = new Vector2(0, 0);
            indexLabel.alignment = TextAnchor.MiddleLeft;

            // Позиция
            var posLabel = UIBuilder.CreateLabel("PosLabel", item.transform,
                $"({point.position.x:F2}, {point.position.y:F2}, {point.position.z:F2})", _font, 16);
            var posRt = posLabel.GetComponent<RectTransform>();
            posRt.anchorMin = new Vector2(0.15f, 0);
            posRt.anchorMax = new Vector2(0.7f, 1);
            posRt.offsetMin = new Vector2(10, 0);
            posRt.offsetMax = new Vector2(0, 0);
            posLabel.alignment = TextAnchor.MiddleLeft;

            // Скорость
            var velLabel = UIBuilder.CreateLabel("VelLabel", item.transform,
                $"v: {point.targetVelocity:F2}", _font, 14);
            var velRt = velLabel.GetComponent<RectTransform>();
            velRt.anchorMin = new Vector2(0.75f, 0);
            velRt.anchorMax = new Vector2(1f, 1);
            velRt.offsetMin = new Vector2(10, 0);
            velRt.offsetMax = new Vector2(-10, 0);
            velLabel.alignment = TextAnchor.MiddleLeft;
            velLabel.color = new Color(0.7f, 0.7f, 0.7f, 1f);

            // Создаем разделительную линию
            var separator = UIBuilder.CreatePanel("Separator", item.transform);
            var sepRt = separator.GetComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0, 0);
            sepRt.anchorMax = new Vector2(1, 0);
            sepRt.sizeDelta = new Vector2(0, 1);

            var sepImage = separator.AddComponent<Image>();
            sepImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            return item;
        }

        private void OnItemClick(int index)
        {
            var manager = MissionManager.Instance;
            if (manager != null && index >= 0 && index < manager.Points.Count)
            {
                manager.SelectPoint(index);
            }
        }

        private void ShowContextMenu(int index)
        {
            // Создаем простое контекстное меню через диалог подтверждения
            // В реальном проекте здесь лучше использовать полноценную систему контекстных меню
            var options = new List<string> { "Delete", "Move Up", "Move Down", "Duplicate" };
            // Здесь можно показать контекстное меню, но для простоты используем Log
            Debug.Log($"Context menu for point {index + 1}. Options: {string.Join(", ", options)}");

            // Пример реализации через диалог (если есть система диалогов)
            // DialogSystem.ShowOptions(options, (selected) => {
            //     HandleContextMenuAction(selected, index);
            // });
        }

        private void HandleContextMenuAction(string action, int index)
        {
            var manager = MissionManager.Instance;
            if (manager == null) return;

            switch (action)
            {
                case "Delete":
                    manager.DeletePoint(index);
                    break;
                case "Move Up":
                    manager.MovePointUp(index);
                    break;
                case "Move Down":
                    manager.MovePointDown(index);
                    break;
                case "Duplicate":
                    var point = manager.Points[index];
                    manager.AddPoint(point.position + new Vector3(0.5f, 0, 0.5f), point.targetVelocity);
                    break;
            }
        }

        public void OnPointsChanged()
        {
            var manager = MissionManager.Instance;
            if (manager == null) return;

            var points = manager.Points;

            // Очищаем старые элементы
            foreach (var item in _pointItems)
            {
                if (item != null)
                    GameObject.Destroy(item);
            }
            _pointItems.Clear();

            // Если точек нет, показываем сообщение
            if (points.Count == 0)
            {
                ShowEmptyMessage();
                return;
            }

            // Создаем новые элементы
            for (int i = 0; i < points.Count; i++)
            {
                var item = CreatePointItem(i, points[i]);
                _pointItems.Add(item);

                // Если это выбранная точка - подсвечиваем
                if (i == _selectedIndex)
                {
                    HighlightItem(item, true);
                }
            }

            // Обновляем Content размер
            UpdateContentSize();

            // Прокручиваем к выбранному элементу
            if (_selectedIndex >= 0 && _selectedIndex < _pointItems.Count)
            {
                ScrollToItem(_selectedIndex);
            }
        }

        private void ShowEmptyMessage()
        {
            var emptyLabel = UIBuilder.CreateLabel("EmptyMessage", _content.transform,
                "No points added.\nClick on the ground to add points.", _font, 18);
            var rt = emptyLabel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.offsetMin = new Vector2(20, -20);
            rt.offsetMax = new Vector2(-20, 20);
            emptyLabel.alignment = TextAnchor.MiddleCenter;
            emptyLabel.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);

            _pointItems.Add(emptyLabel.gameObject);
        }

        private void UpdateContentSize()
        {
            var contentRt = _content.GetComponent<RectTransform>();
            float height = _pointItems.Count * 42 + 10; // 40 + отступы
            contentRt.sizeDelta = new Vector2(0, height);
        }

        public void OnPointSelected(int index)
        {
            // Снимаем выделение со старого элемента
            if (_selectedIndex >= 0 && _selectedIndex < _pointItems.Count)
            {
                HighlightItem(_pointItems[_selectedIndex], false);
            }

            _selectedIndex = index;

            // Выделяем новый элемент
            if (index >= 0 && index < _pointItems.Count)
            {
                HighlightItem(_pointItems[index], true);
                ScrollToItem(index);
            }
        }

        private void HighlightItem(GameObject item, bool selected)
        {
            if (item == null) return;

            var bg = item.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = selected ? _selectedColor : _defaultColor;
            }
        }

        private void ScrollToItem(int index)
        {
            if (_scrollRect == null || _content == null) return;

            // Прокручиваем к элементу
            float elementHeight = 42f;
            float targetPos = index * elementHeight;
            float contentHeight = _content.GetComponent<RectTransform>().rect.height;
            float viewportHeight = _scrollRect.viewport.rect.height;

            if (contentHeight <= viewportHeight) return;

            float normalizedPos = targetPos / (contentHeight - viewportHeight);
            _scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(normalizedPos);
        }

        private void SubscribeToEvents()
        {
            var manager = MissionManager.Instance;
            if (manager != null)
            {
                // Подписываемся на события
                manager.OnPointsChanged += OnPointsChanged;
                manager.OnPointSelected += OnPointSelected;
            }
        }

        public void Cleanup()
        {
            var manager = MissionManager.Instance;
            if (manager != null)
            {
                manager.OnPointsChanged -= OnPointsChanged;
                manager.OnPointSelected -= OnPointSelected;
            }

            foreach (var item in _pointItems)
            {
                if (item != null)
                    GameObject.Destroy(item);
            }
            _pointItems.Clear();

            if (_panel != null)
                GameObject.Destroy(_panel);
        }
    }
}