using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using UnityEngine;

public class RSMACable : MonoBehaviour
{
    public int cableId;
    public Rigidbody mainBody;        // Rigidbody дрона
    public Rigidbody connectedBody;   // Rigidbody груза

    [Header("Параметры троса")]
    public float restLength = 2.0f;   // Свободная длина троса (м)
    public float stiffness = 1000.0f; // Жесткость троса (k), Н/м
    public float damping = 35.0f;     // Демпфирование (c), Н·с/м
    public float maxForce = 200.0f;   // Ограничение максимальной силы (защита от численного взрыва)

    private LineRenderer lineRenderer;
    private RSMA.uDTP.Topics.Float32 cableForce;
    public float currentForce { get; private set; }

    void Start()
    {
        // Скрипт управляет физикой вручную в InitializeCable()
    }

    public void InitializeCable()
    {
        if (mainBody == null || connectedBody == null)
        {
            Debug.LogError($"[RSMACable_{cableId}] Ошибка: mainBody или connectedBody не заданы!");
            return;
        }

        // Если остался старый ConfigurableJoint — гарантированно удаляем его
        ConfigurableJoint oldJoint = mainBody.gameObject.GetComponent<ConfigurableJoint>();
        if (oldJoint != null)
        {
            Destroy(oldJoint);
        }

        // Настройка визуального отображения (LineRenderer)
        lineRenderer = gameObject.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.startWidth = 0.025f;
        lineRenderer.endWidth = 0.025f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = Color.black };

        // Инициализация топика RSMA
        cableForce = new RSMA.uDTP.Topics.Float32();
        cableForce.value = 0.0f;
        cableForce.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish($"CableForce_{cableId}", cableForce);
    }

    void FixedUpdate()
    {
        if (mainBody == null || connectedBody == null) return;

        // 1. Отрисовка троса между центрами масс дрона и груза
        lineRenderer.SetPosition(0, mainBody.position);
        lineRenderer.SetPosition(1, connectedBody.position);

        // 2. Расчет вектора расстояния (от груза к дрону)
        Vector3 delta = mainBody.position - connectedBody.position;
        float currentDistance = delta.magnitude;

        // 3. Вычисление физики троса (Пружина + Демпфер Гука)
        if (currentDistance > restLength)
        {
            float stretch = currentDistance - restLength;
            Vector3 direction = delta / currentDistance; // Нормализованный вектор направления

            // Относительная скорость между дроном и грузом по оси троса
#if UNITY_2023_1_OR_NEWER
            Vector3 relVel = mainBody.linearVelocity - connectedBody.linearVelocity;
#else
            Vector3 relVel = mainBody.velocity - connectedBody.velocity;
#endif
            float vRel = Vector3.Dot(relVel, direction);

            // Сила натяжения (работает только на растяжение, т.е. F >= 0)
            float forceMagnitude = (stiffness * stretch) + (damping * vRel);
            currentForce = Mathf.Clamp(forceMagnitude, 0.0f, maxForce);

            // Вектор силы
            Vector3 forceVector = direction * currentForce;

            // 4. Прямое приложение сил к Rigidbody в PhysX
            // - К дрону прикладываем силу, направленную ВНИЗ (к грузу)
            // - К грузу прикладываем силу, направленную ВВЕРХ (к дрону)
            mainBody.AddForce(-forceVector, ForceMode.Force);
            connectedBody.AddForce(forceVector, ForceMode.Force);
        }
        else
        {
            currentForce = 0.0f; // Трос провис
        }

        // 5. Публикация силы в RSMA Broker для Python
        cableForce.value = currentForce;
        cableForce.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish($"CableForce_{cableId}", cableForce);
    }
}