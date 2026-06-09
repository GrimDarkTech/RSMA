using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace RSMA.GUI
{
    public static class UIBuilder
    {
        // Базовый метод для создания панели
        public static GameObject CreatePanel(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        // Создание элемента в сетке (Grid)
        // row, col: индекс (с 0)
        // rowSpan, colSpan: сколько ячеек занимает
        // totalRows, totalCols: общее количество строк и столбцов в сетке
        public static void PlaceInGrid(GameObject element, int row, int col, int rowSpan, int colSpan, int totalRows, int totalCols)
        {
            RectTransform rt = element.GetComponent<RectTransform>();
            if (rt == null) return;

            // Расчет размеров и позиции в процентах (0.0 - 1.0)
            float widthPerCell = 1f / totalCols;
            float heightPerCell = 1f / totalRows;

            // Установка якорей (Anchors) для растяжения
            rt.anchorMin = new Vector2(col * widthPerCell, 1f - (row + rowSpan) * heightPerCell);
            rt.anchorMax = new Vector2((col + colSpan) * widthPerCell, 1f - row * heightPerCell);

            // Сброс отступов
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Text CreateLabel(string name, Transform parent, string text, Font font, int fontSize = 14)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return t;
        }

        public static Button CreateButton(string name, Transform parent, string labelText, Font font, int fontSize = 14, UnityAction onClickAction = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(go.transform, false);
            Text textComp = textObj.AddComponent<Text>();
            textComp.text = labelText;
            textComp.font = font;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.color = Color.white;
            textComp.fontSize = fontSize;

            // Растягиваем текст по кнопке
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            btn.onClick.AddListener(onClickAction);
            return btn;
        }
        public static InputField CreateInputField(string name, Transform parent, Font font, int fontSize = 14, string placeholderText = "Введите текст...")
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            // Фон поля ввода
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f); // Светлый фон

            // Сам компонент InputField
            InputField input = go.AddComponent<InputField>();

            // Создаем дочерний объект для отображения текста ввода
            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(go.transform, false);
            Text textComp = textObj.AddComponent<Text>();
            textComp.font = font;
            textComp.color = Color.black;
            textComp.alignment = TextAnchor.MiddleLeft;
            textComp.fontSize = fontSize;

            // Настройка RectTransform для текста (занимает всё пространство поля)
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(5, 0); // Небольшой отступ слева
            textRt.offsetMax = Vector2.zero;

            input.textComponent = textComp;

            // (Опционально) Создаем placeholder
            GameObject placeholderObj = new GameObject("Placeholder", typeof(RectTransform));
            placeholderObj.transform.SetParent(go.transform, false);
            Text placeholderComp = placeholderObj.AddComponent<Text>();
            placeholderComp.text = placeholderText;
            placeholderComp.font = font;
            placeholderComp.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderComp.alignment = TextAnchor.MiddleLeft;

            input.placeholder = placeholderComp;

            return input;
        }
    }
}