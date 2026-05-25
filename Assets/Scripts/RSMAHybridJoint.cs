using UnityEngine;

public abstract class RSMAHybridJoint : MonoBehaviour
{
    protected Rigidbody ModuleRigidbody { get; private set; }
    protected ArticulationBody ArticulationBody { get; private set; }

    public bool IsArticulation => ArticulationBody != null;

    protected virtual void Awake()
    {
        ArticulationBody = GetComponent<ArticulationBody>();
        ModuleRigidbody = GetComponent<Rigidbody>();

        if (ArticulationBody == null && ModuleRigidbody == null)
        {
            Debug.LogError($"[RSMA] На объекте {gameObject.name} отсутствует и Rigidbody, и ArticulationBody! Компонент {GetType().Name} отключен.");
            enabled = false;
        }
    }
}