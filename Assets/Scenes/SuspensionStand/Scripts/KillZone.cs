using UnityEngine;

namespace SuspensionTest
{
    public class KillZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
           if(!other.gameObject.TryGetComponent<Camera>(out Camera camera))
            {
                Destroy(other.gameObject);
            }
        }
    }
}


