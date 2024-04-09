using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SuspensionTest
{
    public class KillZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            RSMADrone drone = other.gameObject.GetComponent<RSMADrone>();

            if (drone == null)
            {
                Destroy(other.gameObject);
            }
        }
    }
}


