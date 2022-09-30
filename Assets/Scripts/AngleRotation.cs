using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngleRotation : MonoBehaviour
{
    [SerializeField] private Vector3 rotation;
    [SerializeField] private bool isRotate;
    [SerializeField] private bool isSetAngle;
    [SerializeField] private Vector3 curentRotation;

    private Vector3 newRotation;

    public void rotateAngle(Vector3 deltaRotation)
    {
        curentRotation = curentRotation + deltaRotation;
        transform.rotation = Quaternion.Euler(curentRotation);
    }
    public void setAngle(Vector3 angle)
    {
        transform.rotation = Quaternion.Euler(angle);
    }
    private void Start()
    {
        Vector3 curRotation = transform.rotation.eulerAngles;
    }
    void Update()
    {
        if (isRotate)
        {
            rotateAngle(rotation * Time.deltaTime);
        }
        else if (isSetAngle)
        {
            setAngle(rotation);
        }
    }
}
