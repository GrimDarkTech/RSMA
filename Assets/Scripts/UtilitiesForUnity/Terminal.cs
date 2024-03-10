using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Terminal : MonoBehaviour
{
    public bool isEnabled;

    public Canvas terminal;

    private InputField _inputField;

    [SerializeField] private Text _text;

    private int numOfLines = 1;

    private void Start()
    {
        _inputField= GetComponentInChildren<InputField>();
    }
    public void TurnOnOff()
    {
        if(terminal != null)
        {
            terminal.enabled = !isEnabled;
        }
        isEnabled = !isEnabled;
    }

    public void Update()
    {
        if (isEnabled && Input.GetKeyDown(KeyCode.Return))
        {
            numOfLines++;

            if (numOfLines > 28)
            {
                _text.text = "RSMA..";
                numOfLines = 1;
            }

            string[] splited = _inputField.text.Split(" ");

            if (splited.Length > 0 && splited[0] != "")
            {
                //RSMA Server.execute
                _text.text += $"\n{_inputField.text}";
            }
            else
            {
                _text.text += $"\nInvalid command";
            }

            _inputField.text = "";
        }
    }
}
