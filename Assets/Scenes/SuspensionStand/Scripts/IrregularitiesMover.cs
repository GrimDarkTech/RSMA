using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SuspensionTest
{
    public class IrregularitiesMover : MonoBehaviour
    {
        public Vector3 direction;
        public float velocity;

        private void Update()
        {
            if (velocity > 0)
            {
                gameObject.transform.position += velocity * Time.deltaTime * direction;
            }
        }
    }
}