using System.Collections.Generic;
using UnityEngine;

public class RSMASwarmEnvironment : MonoBehaviour
{
    [Header("Префабы и ссылки")]
    public GameObject dronePrefab;
    public GameObject payloadPrefab;

    [Header("Параметры роя")]
    public int numDrones = 6;
    public float payloadMass = 12.0f;
    public float radius = 1.414f;
    public float cableLength = 2.0f;

    [HideInInspector] public GameObject payloadInstance;
    [HideInInspector] public List<Quadrocopter> droneInstances = new List<Quadrocopter>();
    [HideInInspector] public List<RSMACable> cableInstances = new List<RSMACable>();

    void Start()
    {
        BuildSwarmScene();
    }

    public void BuildSwarmScene()
    {
        // 1. Создание груза
        if (payloadPrefab != null)
        {
            payloadInstance = Instantiate(payloadPrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            payloadInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            payloadInstance.transform.position = new Vector3(0, 0.5f, 0); // Исходная позиция на земле
            payloadInstance.transform.localScale = Vector3.one * 0.4f;
            payloadInstance.GetComponent<Renderer>().material.color = Color.red;
        }

        Rigidbody payloadRb = payloadInstance.GetComponent<Rigidbody>();
        if (payloadRb == null) payloadRb = payloadInstance.AddComponent<Rigidbody>();
        payloadRb.mass = payloadMass;

        // В Unity 2023+ используем linearDamping, для старых версий - drag
#if UNITY_2023_1_OR_NEWER
        payloadRb.linearDamping = 0.2f;
#else
        payloadRb.drag = 0.2f;
#endif

        // 2. Генерация дронов по кругу
        float angleStep = 360.0f / numDrones;
        float angleOffset = (numDrones == 4) ? 45.0f : 0.0f;

        for (int i = 0; i < numDrones; i++)
        {
            int droneId = i + 1;
            float angleRad = (i * angleStep + angleOffset) * Mathf.Deg2Rad;

            float dx = radius * Mathf.Cos(angleRad);
            float dz = radius * Mathf.Sin(angleRad);

            // Начальное положение дрона над грузом
            Vector3 initPos = new Vector3(dx, cableLength + 0.5f, dz);

            GameObject dObj;
            if (dronePrefab != null)
            {
                dObj = Instantiate(dronePrefab, initPos, Quaternion.identity);
            }
            else
            {
                dObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dObj.transform.position = initPos;
                dObj.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
            }

            dObj.name = $"Quadrocopter_{droneId}";

            // Убеждаемся, что у дрона есть Rigidbody
            Rigidbody droneRb = dObj.GetComponent<Rigidbody>();
            if (droneRb == null)
            {
                droneRb = dObj.AddComponent<Rigidbody>();
                droneRb.mass = 2.5f; // Масса дрона из config.py
            }

            Quadrocopter droneScript = dObj.GetComponent<Quadrocopter>();
            if (droneScript == null) droneScript = dObj.AddComponent<Quadrocopter>();

            droneScript.droneId = droneId;
            droneInstances.Add(droneScript);

            // 3. Создание троса и безопасная инициализация
            GameObject cableObj = new GameObject($"Cable_{droneId}");
            cableObj.transform.SetParent(dObj.transform);

            RSMACable cableScript = cableObj.AddComponent<RSMACable>();

            cableScript.cableId = droneId;
            cableScript.mainBody = droneRb;
            cableScript.connectedBody = payloadRb;
            cableScript.restLength = cableLength;

            // Реалистичные параметры самописной пружины троса
            cableScript.stiffness = 1000.0f; // Н/м
            cableScript.damping = 35.0f;     // Н·с/м
            cableScript.maxForce = 250.0f;   // Н

            cableScript.InitializeCable();
            cableInstances.Add(cableScript);
        }

        Debug.Log($"[RSMA Engine] Сцена успешно собрана: {numDrones} дронов, груз {payloadMass} кг.");
    }
}