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
        _inputField = GetComponentInChildren<InputField>();
        CommandHandler.terminal = this;
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

            string response = CommandHandler.Execute(_inputField.text);

            _text.text += $"\n{response}";

            _inputField.text = "";

            _inputField.ActivateInputField();
        }
        else if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.T))
        {
            TurnOnOff();
        }
    }

    public void Print(string text)
    {
            numOfLines++;

            if (numOfLines > 28)
            {
                _text.text = "RSMA..";
                numOfLines = 1;
            }

            _text.text += text;
    }
}
