using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModuleBaseScript : MonoBehaviour
{
    void Start()
    {
        Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
    }

    void Update()
    {
        
    }
}
