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

    private int _numOfLines = 1;

    private string _lastCommand = "";

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
            _numOfLines++;

            if (_numOfLines > 28)
            {
                _text.text = "RSMA..";
                _numOfLines = 1;
            }

            string response = CommandHandler.Execute(_inputField.text);

            _text.text += $"\n{response}";

            _lastCommand = _inputField.text;

            _inputField.text = "";

            _inputField.ActivateInputField();
        }
        else if (isEnabled && Input.GetKeyDown(KeyCode.UpArrow))
        {
            _inputField.text = _lastCommand;
        }
        else if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.T))
        {
            TurnOnOff();
        }
    }

    public void Print(string text)
    {
        _numOfLines++;

            if (_numOfLines > 28)
            {
                _text.text = "RSMA..";
            _numOfLines = 1;
            }

            _text.text += text;
    }
}
