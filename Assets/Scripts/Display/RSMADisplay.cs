using UnityEngine.UI;
using UnityEngine;
using TMPro;

/// <summary>
/// Implements properties and functionality of text display
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Electronics/Setting_up_display.md")]
public class RSMADisplay : RSMADataTransferSlave
{
    /// <summary>
    /// TextMesh component used to display text
    /// </summary>
    public TextMeshPro text;

    /// <summary>
    /// Font size in millimeters
    /// </summary>
    public float fontSize = 3f;

    /// <summary>
    /// Font color in RGB
    /// </summary>
    public Color fontColor = Color.white;

    /// <summary>
    /// Character spacing in em
    /// </summary>
    public float characterSpacing = 12f;

    /// <summary>
    /// Line spacing in em
    /// </summary>
    public float lineSpacing = 24f;

    /// <summary>
    /// Lower left corner of the display
    /// </summary>
    public Vector3 lowerLeftAnchor;

    /// <summary>
    /// Upper right corner of the display
    /// </summary>
    public Vector3 upperRightAnchor;

    /// <summary>
    /// If True, displays the position of the anchors
    /// </summary>
    public bool isDrawAnchors = false;

    [ContextMenu("Apply changes")]
    private void OnEnable()
    {
        if(text != null)
        {
            text.transform.position = transform.TransformPoint(lowerLeftAnchor);
            text.rectTransform.sizeDelta = new Vector2(lowerLeftAnchor.x - upperRightAnchor.x, upperRightAnchor.y - lowerLeftAnchor.y);
            text.rectTransform.anchorMax = Vector2.zero;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.pivot = Vector2.zero;
            text.overflowMode = TextOverflowModes.Truncate;
            text.margin = Vector4.zero;
            text.fontSize = fontSize / 75;
            text.color = fontColor;
            text.characterSpacing = characterSpacing - 12f;
            text.lineSpacing = lineSpacing - 24f;
        }
    }
    /// <summary>
    /// Sets display text
    /// </summary>
    /// <param name="recivedData">Data</param>
    public override void ReciveData(string recivedData)
    {
        text.text = recivedData;
    }

    private void OnDrawGizmos()
    {
        if (isDrawAnchors)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.TransformPoint(lowerLeftAnchor), 0.002f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.TransformPoint(upperRightAnchor), 0.002f);
        }
    }
}
