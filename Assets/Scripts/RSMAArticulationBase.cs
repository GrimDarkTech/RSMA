using UnityEngine;

/// <summary>
/// Controls of an object's position and rotation through physics simulation. Uses ArticulationBody to simulate physics for robotics.
/// </summary>
[RequireComponent(typeof(ArticulationBody))]
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Mechanics/Setting_up_the_physics_of_models.md")]
public class RSMAArticulationBase : MonoBehaviour
{
    private ArticulationBody _articulationBody;

    /// <summary>
    /// The mass of body in kilograms
    /// </summary>
    public float mass = 0.5f;

    /// <summary>
    /// The center of mass relative to the transform's origin position in meters
    /// </summary>
    public Vector3 centerOfMassPosition = Vector3.zero;

    /// <summary>
    /// If True, the rendering of the position of the center of mass of the body is enabled. 
    /// The center of mass is displayed as a yellow sphere.
    /// </summary>
    public bool isDrawCenterOfMass = true;


    private void Start()
    {
        if (_articulationBody == null)
        {
            _articulationBody = gameObject.GetComponent<ArticulationBody>();
        }

        if (mass <= 0)
        {
            mass = 0.01f;
        }

        // В ArticulationBody масса и центр масс задаются через соответствующие свойства.
        _articulationBody.mass = mass;
        _articulationBody.centerOfMass = centerOfMassPosition;

        // У ArticulationBody нет метода WakeUp(), так как они рассчитываются в рамках единого редуктора (Featherstone Solver).
        // Вместо этого используется изменение состояния сна через свойства, если необходимо, 
        // но обычно они активны по умолчанию при старте, если дерево сочленений инициализировано.
    }

    private void OnDrawGizmos()
    {
        if (isDrawCenterOfMass)
        {
            Gizmos.color = Color.yellow;
            // Используем transform.TransformPoint для сохранения логики отрисовки относительно локального origin
            Gizmos.DrawSphere(transform.TransformPoint(centerOfMassPosition), 0.002f);
        }
    }
}