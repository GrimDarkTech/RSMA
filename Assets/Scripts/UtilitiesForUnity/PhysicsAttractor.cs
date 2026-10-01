using UnityEngine;

public class PhysicsAttractor : MonoBehaviour
{
    [Header("Ссылки")]
    public Rigidbody targetBody;

    [Header("Настройки позиции (Линейное притяжение)")]
    public float positionKp = 15.0f;
    public float linearDamping = 5.0f;

    [Header("Настройки поворота (Линейный разворот)")]
    public float rotationKp = 10.0f;
    public float angularDamping = 3.0f;

    [Header("Ограничения (Лимиты)")]
    public float maxVelocity = 10.0f;
    public float maxAngularVelocity = 5.0f;

    void FixedUpdate()
    {
        if (targetBody == null) return;

        Vector3 positionError = transform.position - targetBody.position;


        Vector3 linearForce = (positionError * positionKp) - (targetBody.linearVelocity * linearDamping);

        targetBody.AddForce(linearForce, ForceMode.Acceleration);

        // Ограничение максимальной линейной скорости для плавности
        if (targetBody.linearVelocity.magnitude > maxVelocity)
        {
            targetBody.linearVelocity = targetBody.linearVelocity.normalized * maxVelocity;
        }

        // --- 2. Управление ориентацией (Сила разворота + Вязкое трение) ---
        // Вычисляем разницу во вращении через Quaternion
        Quaternion rotationError = transform.rotation * Quaternion.Inverse(targetBody.rotation);
        rotationError.ToAngleAxis(out float angleInDegrees, out Vector3 axis);

        if (angleInDegrees > 180.0f)
            angleInDegrees -= 360.0f; // Нормализуем угол в диапазон [-180, 180]

        if (Mathf.Abs(angleInDegrees) > 0.001f)
        {
            // Переводим ось в локальные/мировые угловые скорости
            Vector3 rotationAxisRad = axis.normalized * (angleInDegrees * Mathf.Deg2Rad);

            // Момент силы зависит линейно от разности угла (Kp), 
            // а угловое вязкое трение гасит вращение (angularVelocity * damping)
            Vector3 angularTorque = (rotationAxisRad * rotationKp) - (targetBody.angularVelocity * angularDamping);

            targetBody.AddTorque(angularTorque, ForceMode.Acceleration);
        }

        // Ограничение максимальной угловой скорости
        if (targetBody.angularVelocity.magnitude > maxAngularVelocity)
        {
            targetBody.angularVelocity = targetBody.angularVelocity.normalized * maxAngularVelocity;
        }
    }
}