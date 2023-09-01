using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class StateActivator : MonoBehaviour
{
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    protected virtual void SwitchOn()
    {
        Debug.LogFormat("Состояние переключателя: Включено");
    }

    protected virtual void SwitchOff()
    {
        Debug.LogFormat("Состояние переключателя: Выключено");
    }
}
