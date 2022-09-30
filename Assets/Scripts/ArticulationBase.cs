using System;
using UnityEngine;

public class ArticulationBase : MonoBehaviour
{
    public ArticulationPart[] articulationArray = new ArticulationPart[100];
    public Vector3 articulationOrigin;

    float rotator = 0;

    private void Start()
    {
        GameObject currentGameObject;
        Quaternion currentRotation;
        Vector3 currentVector;
        Vector3 currentOrigin;

        articulationArray[0].SetOrigin(articulationOrigin);

        for (int i = 0; i < articulationArray.Length; i++)
        {
            articulationArray[i].SetVector(articulationArray[i].GetGameObject().transform.forward * articulationArray[i].GetMagnitude());
            if (i > 0)
            {
                articulationArray[i].SetOrigin(articulationArray[i].GetOrigin() + articulationArray[i - 1].GetVector());
            }

            currentGameObject = articulationArray[i].GetGameObject();
            currentRotation = articulationArray[i].GetRotation();
            currentVector = articulationArray[i].GetVector();
            currentOrigin = articulationArray[i].GetOrigin();

            currentGameObject.transform.position = currentOrigin;
            currentGameObject.transform.rotation = currentRotation;
        }
    }
    private void FixedUpdate()
    {
        GameObject currentGameObject;
        Quaternion currentRotation;
        Vector3 currentVector;
        Vector3 currentOrigin;

        

        articulationArray[0].SetOrigin(articulationOrigin);

        for (int i = 0; i < articulationArray.Length; i++)
        {
            articulationArray[i].SetVector(articulationArray[i].GetGameObject().transform.forward * articulationArray[i].GetMagnitude());
            if (i > 0)
            {
                articulationArray[i].SetOrigin(articulationArray[i - 1].GetOrigin() + articulationArray[i - 1].GetVector());
            }
            currentGameObject = articulationArray[i].GetGameObject();
            currentRotation = articulationArray[i].GetRotation();
            currentVector = articulationArray[i].GetVector();
            currentOrigin = articulationArray[i].GetOrigin();

            currentGameObject.transform.position = currentOrigin;
            currentGameObject.transform.rotation = currentRotation;
        }

        rotator = rotator + 0.3f;
        articulationArray[1].SetRotation(Quaternion.Euler(-30, rotator, 0));

        articulationArray[3].SetRotation(Quaternion.Euler(rotator, 0, 0));
        articulationArray[4].SetRotation(Quaternion.Euler(0.5f*rotator, 0, 0));
        articulationArray[5].SetRotation(Quaternion.Euler(0.1f * rotator, 0, 0));
        articulationArray[6].SetRotation(Quaternion.Euler(-0.2f * rotator, 0, 0));
        articulationArray[7].SetRotation(Quaternion.Euler(0.3f * rotator, 0, 0));
    }

}
[Serializable]
public class ArticulationPart
{
    [SerializeField] private GameObject gameObject;
    [SerializeField] private Vector3 origin;
    [SerializeField] private Quaternion rotation;
    [SerializeField] private float magnitude;
    private Vector3 vector;

    public void SetOrigin(Vector3 newOrigin)
    {
        origin = newOrigin;
    }
    public Vector3 GetOrigin()
    {
        return origin;
    }
    public void SetGameObject(GameObject newGameObject)
    {
        gameObject = newGameObject;
    }
    public GameObject GetGameObject()
    {
        return gameObject;
    }
    public void SetRotation(Quaternion newRotation)
    {
        rotation = newRotation;
    }
    public Quaternion GetRotation()
    {
        return rotation;
    }
    public void SetVector(Vector3 newVector)
    {
        vector = newVector;
    }
    public Vector3 GetVector()
    {
        return vector;
    }
    public void SetMagnitude(float newMagnitude)
    {
        magnitude = newMagnitude;
    }
    public float GetMagnitude()
    {
        return magnitude;
    }
}
