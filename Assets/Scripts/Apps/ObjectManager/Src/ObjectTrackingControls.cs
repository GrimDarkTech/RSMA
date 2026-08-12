using System.Collections.Generic;
using System.Globalization;
using RSMA.GUI;
using UnityEngine;
using UnityEngine.UI;

namespace RSMA.ObjectManager.UI
{
    public class ObjectTrackingControls
    {
        private Transform _parent;
        private Font _font;
        private GameObject _panel;
        private Transform _listContent;

        private CameraFollower _cameraFollower;
        private GameObject _selectedObject;
        private float _lastClickTime;
        private const float DoubleClickThreshold = 0.3f;

        // Поля ввода для параметров Offset и RotationOffset
        private InputField _offsetXInput;
        private InputField _offsetYInput;
        private InputField _offsetZInput;

        private InputField _rotXInput;
        private InputField _rotYInput;
        private InputField _rotZInput;

        public string TrackableTag { get; set; } = "Trackable";

        public ObjectTrackingControls(Transform parent, Font font)
        {
            _parent = parent;
            _font = font;

            InitCameraFollower();
            CreatePanel();
            SyncSettingsFromCamera();
        }

        private void InitCameraFollower()
        {
            // 1. Пробуем получить через Camera.main
            Camera mainCam = Camera.main;

            // 2. Если null, ищем любую активную камеру на сцене
            if (mainCam == null)
            {
                mainCam = Object.FindFirstObjectByType<Camera>();
            }

            if (mainCam != null)
            {
                _cameraFollower = mainCam.GetComponent<CameraFollower>();
                if (_cameraFollower == null)
                    _cameraFollower = mainCam.gameObject.AddComponent<CameraFollower>();
            }
            else
            {
                Debug.LogError("[ObjectTrackingControls] Не удалось найти активную камеру в сцене!");
            }
        }

        private void CreatePanel()
        {
            _panel = UIBuilder.CreatePanel("TrackingControlsPanel", _parent);
            RectTransform rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _panel.GetComponent<Image>() ?? _panel.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

            // 1. Кнопки управления (Grid 0..2)
            var refreshBtn = UIBuilder.CreateButton("RefreshBtn", _panel.transform, "Refresh List", _font, 14, RefreshObjectList);
            UIBuilder.PlaceInGrid(refreshBtn.gameObject, 0, 0, 1, 1, 10, 1);

            var freeCamBtn = UIBuilder.CreateButton("FreeCamBtn", _panel.transform, "Free Camera", _font, 14, () =>
            {
                _cameraFollower?.EnableFreeCamera();
            });
            UIBuilder.PlaceInGrid(freeCamBtn.gameObject, 1, 0, 1, 1, 10, 1);

            var followBtn = UIBuilder.CreateButton("FollowBtn", _panel.transform, "Follow Object", _font, 14, () =>
            {
                if (_selectedObject != null)
                    _cameraFollower?.StartFollowing(_selectedObject.transform);
            });
            UIBuilder.PlaceInGrid(followBtn.gameObject, 2, 0, 1, 1, 10, 1);

            // 2. Секция редактирования параметров следования (Grid 3..5)
            CreateSettingsInputs();

            // 3. Контейнер для списка объектов (Нижняя часть панели)
            GameObject listContainer = new GameObject("ObjectListContainer", typeof(RectTransform));
            listContainer.transform.SetParent(_panel.transform, false);
            RectTransform listRt = listContainer.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0.05f, 0.05f);
            listRt.anchorMax = new Vector2(0.95f, 0.25f);
            listRt.offsetMin = Vector2.zero;
            listRt.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = listContainer.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;

            _listContent = listContainer.transform;
        }

        private void CreateSettingsInputs()
        {
            // Подпись и поля ввода Offset (X, Y, Z)
            var offsetLabel = UIBuilder.CreateLabel("OffsetLabel", _panel.transform, "Follow Offset (X, Y, Z):", _font, 12);
            UIBuilder.PlaceInGrid(offsetLabel.gameObject, 3, 0, 1, 1, 10, 1);

            GameObject offsetFieldsRow = CreateThreeInputRow("OffsetRow", out _offsetXInput, out _offsetYInput, out _offsetZInput);
            UIBuilder.PlaceInGrid(offsetFieldsRow, 4, 0, 1, 1, 10, 1);

            // Подпись и поля ввода Rotation Offset (X, Y, Z)
            var rotLabel = UIBuilder.CreateLabel("RotLabel", _panel.transform, "Rotation Offset (Pitch, Yaw, Roll):", _font, 12);
            UIBuilder.PlaceInGrid(rotLabel.gameObject, 5, 0, 1, 1, 10, 1);

            GameObject rotFieldsRow = CreateThreeInputRow("RotRow", out _rotXInput, out _rotYInput, out _rotZInput);
            UIBuilder.PlaceInGrid(rotFieldsRow, 6, 0, 1, 1, 10, 1);

            // Привязываем слушатели изменения значений
            _offsetXInput.onValueChanged.AddListener(_ => ApplySettingsToCamera());
            _offsetYInput.onValueChanged.AddListener(_ => ApplySettingsToCamera());
            _offsetZInput.onValueChanged.AddListener(_ => ApplySettingsToCamera());

            _rotXInput.onValueChanged.AddListener(_ => ApplySettingsToCamera());
            _rotYInput.onValueChanged.AddListener(_ => ApplySettingsToCamera());
            _rotZInput.onValueChanged.AddListener(_ => ApplySettingsToCamera());
        }

        private GameObject CreateThreeInputRow(string name, out InputField inputX, out InputField inputY, out InputField inputZ)
        {
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(_panel.transform, false);

            HorizontalLayoutGroup hGroup = row.AddComponent<HorizontalLayoutGroup>();
            hGroup.childControlWidth = true;
            hGroup.childForceExpandWidth = true;
            hGroup.spacing = 5f;

            inputX = UIBuilder.CreateInputField($"{name}_X", row.transform, _font, 12);
            inputY = UIBuilder.CreateInputField($"{name}_Y", row.transform, _font, 12);
            inputZ = UIBuilder.CreateInputField($"{name}_Z", row.transform, _font, 12);

            return row;
        }

        /// <summary>
        /// Выгружает текущие параметры из CameraFollower в UI.
        /// </summary>
        public void SyncSettingsFromCamera()
        {
            if (_cameraFollower == null) return;

            Vector3 offset = _cameraFollower.offset;
            Vector3 rot = _cameraFollower.rotationOffset;

            if (_offsetXInput != null) _offsetXInput.text = offset.x.ToString("F1", CultureInfo.InvariantCulture);
            if (_offsetYInput != null) _offsetYInput.text = offset.y.ToString("F1", CultureInfo.InvariantCulture);
            if (_offsetZInput != null) _offsetZInput.text = offset.z.ToString("F1", CultureInfo.InvariantCulture);

            if (_rotXInput != null) _rotXInput.text = rot.x.ToString("F1", CultureInfo.InvariantCulture);
            if (_rotYInput != null) _rotYInput.text = rot.y.ToString("F1", CultureInfo.InvariantCulture);
            if (_rotZInput != null) _rotZInput.text = rot.z.ToString("F1", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Применяет введенные значения из UI в компонент CameraFollower.
        /// </summary>
        private void ApplySettingsToCamera()
        {
            if (_cameraFollower == null) return;

            float ox = ParseFloat(_offsetXInput?.text, _cameraFollower.offset.x);
            float oy = ParseFloat(_offsetYInput?.text, _cameraFollower.offset.y);
            float oz = ParseFloat(_offsetZInput?.text, _cameraFollower.offset.z);

            float rx = ParseFloat(_rotXInput?.text, _cameraFollower.rotationOffset.x);
            float ry = ParseFloat(_rotYInput?.text, _cameraFollower.rotationOffset.y);
            float rz = ParseFloat(_rotZInput?.text, _cameraFollower.rotationOffset.z);

            _cameraFollower.offset = new Vector3(ox, oy, oz);
            _cameraFollower.rotationOffset = new Vector3(rx, ry, rz);
        }

        private float ParseFloat(string value, float fallback)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return fallback;
        }

        public void RefreshObjectList()
        {
            if (_listContent == null) return;

            foreach (Transform child in _listContent)
            {
                Object.Destroy(child.gameObject);
            }

            GameObject[] trackedObjects = GameObject.FindGameObjectsWithTag(TrackableTag);

            foreach (var obj in trackedObjects)
            {
                GameObject targetObj = obj;
                var btn = UIBuilder.CreateButton($"Item_{targetObj.name}", _listContent, targetObj.name, _font, 13, () =>
                {
                    OnObjectClicked(targetObj);
                });

                RectTransform btnRt = btn.GetComponent<RectTransform>();
                btnRt.sizeDelta = new Vector2(0, 30);
            }
        }

        private void OnObjectClicked(GameObject targetObj)
        {
            float timeSinceLastClick = Time.time - _lastClickTime;

            if (timeSinceLastClick <= DoubleClickThreshold && _selectedObject == targetObj)
            {
                _cameraFollower?.FocusOnTarget(targetObj.transform);
            }
            else
            {
                _selectedObject = targetObj;
            }

            _lastClickTime = Time.time;
        }
    }
}