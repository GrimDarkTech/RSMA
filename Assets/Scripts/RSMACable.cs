using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using UnityEngine;

public class RSMACable : MonoBehaviour
{
    public int cableId;
    public Rigidbody mainBody;
    public Rigidbody connectedBody;

    [Header("Параметры троса")]
    public float restLength = 2.0f;  // Из config.py (2.0 м)
    public float stiffness = 2000.0f; // Жесткость троса (k)
    public float damping = 40.0f;     // Демпфирование (c)

    private ConfigurableJoint joint;
    private LineRenderer lineRenderer;

    private RSMA.uDTP.Topics.Float32 cableForce;
    public float currentForce { get; private set; }

    void Start()
    {
        // Создаем и настраиваем Joint физики Unity
        joint = mainBody.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = connectedBody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero;
        joint.connectedAnchor = Vector3.zero;

        // Настройка упругой связи (работает на растяжение как Spring)
        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Limited;

        SoftJointLimit limit = new SoftJointLimit();
        limit.limit = restLength;
        joint.linearLimit = limit;

        JointDrive drive = new JointDrive();
        drive.positionSpring = stiffness;
        drive.positionDamper = damping;
        drive.maximumForce = 1000.0f;

        joint.slerpDrive = drive;

        // Настройка визуального отображения троса
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.03f;
        lineRenderer.endWidth = 0.03f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = Color.black };

        cableForce = new RSMA.uDTP.Topics.Float32();
        cableForce.value = 0;
        cableForce.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish($"CableForce_{cableId}", cableForce);
    }

    void FixedUpdate()
    {
        if (mainBody == null || connectedBody == null) return;

        // Отрисовка линии троса
        lineRenderer.SetPosition(0, mainBody.position);
        lineRenderer.SetPosition(1, connectedBody.position);

        // Расчет силы натяжения троса
        Vector3 delta = mainBody.position - connectedBody.position;
        float currentDistance = delta.magnitude;

        if (currentDistance > restLength)
        {
            float stretch = currentDistance - restLength;
            Vector3 relVel = mainBody.linearVelocity - connectedBody.linearVelocity;
            float vRel = Vector3.Dot(relVel, delta.normalized);

            currentForce = Mathf.Max(0.0f, (stiffness * stretch) + (damping * vRel));
        }
        else
        {
            currentForce = 0.0f; // Трос провис
        }

        cableForce.value = currentForce;
        cableForce.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish($"CableForce_{cableId}", cableForce);
    }
}