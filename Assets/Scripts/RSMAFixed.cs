using UnityEngine;

/// <summary>
/// Адаптивное фиксированное сочленение. 
/// Поддерживает работу как с Rigidbody (через FixedJoint), так и с ArticulationBody.
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Mechanics/Setting_up_fixed_joints.md")]
public class RSMAFixed : RSMAHybridJoint
{
    [Header("Rigidbody Settings")]
    [Tooltip("Используется только если на объекте висит Rigidbody. Если пусто, свяжет с точкой в пространстве.")]
    public Rigidbody connectedBody;
    public ArticulationBody connectedArticulationBody;

    private FixedJoint _fixedJoint;

    protected override void Awake()
    {
        base.Awake(); // Инициализируем базовый класс

        if (!enabled) return;

        // Если это классическая физика, настраиваем FixedJoint
        if (!IsArticulation)
        {
            _fixedJoint = GetComponent<FixedJoint>();
            if (_fixedJoint == null)
            {
                _fixedJoint = gameObject.AddComponent<FixedJoint>();
            }
        }
    }

    private void Start()
    {
        if (IsArticulation)
        {
            InitializeArticulation();
        }
        else
        {
            InitializeRigidbody();
        }
    }

    private void InitializeArticulation()
    {
        // Проверяем иерархию для ArticulationBody
        if (transform.parent != null && transform.parent.GetComponentInParent<ArticulationBody>() != null)
        {
            ArticulationBody.jointType = ArticulationJointType.FixedJoint;
        }
        else
        {
            // Если это Root-тело, сустав не нужен, оно крепится к миру через свойство immovable
            Debug.LogWarning($"[RSMAFixed] {gameObject.name} является корневым (Root) ArticulationBody. Тип FixedJoint игнорируется.");
        }
    }

    private void InitializeRigidbody()
    {
        if (connectedBody != null)
        {
            _fixedJoint.connectedBody = connectedBody;
        }
        else if (connectedArticulationBody != null)
        {
            _fixedJoint.connectedArticulationBody = connectedArticulationBody;
        }
        else
        {
            Debug.LogWarning($"[RSMAFixed] ConnectedBody в {gameObject.name} не задан. Объект зафиксирован в пространстве.");
        }
    }
}