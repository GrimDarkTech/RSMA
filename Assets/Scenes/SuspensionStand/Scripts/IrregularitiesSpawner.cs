using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace SuspensionTest
{
    public class IrregularitiesSpawner : MonoBehaviour
    {
        public GameObject irregularitie;

        bool isSpawning = false;

        public int functionId = 0;

        public float amplitude;

        public float frequency;

        public float offset;

        private IEnumerator routine;

        [ContextMenu("Start")]
        public void Spawn()
        {
            if (!isSpawning)
            {
                routine = SpawnAsync();

                StopCoroutine(routine);

                StartCoroutine(routine);

                isSpawning = true;
            }
        }
        [ContextMenu("Stop")]
        public void Stop()
        {
            StopCoroutine(routine);
            isSpawning = false;
        }

        public void SetFuntion(int id)
        {
            functionId = id;
        }

        private IEnumerator SpawnAsync()
        {
            float timer = 0;

            while (true)
            {
                Vector3 position = transform.position;
                float function = 0;
                if (functionId == 1)
                {
                    function = amplitude * Mathf.Sin(frequency * timer);
                }
                else if (functionId == 2)
                {
                    function = amplitude * Mathf.Asin(Mathf.Sin(frequency * timer));
                }
                else if (functionId == 3)
                {
                    if(Mathf.Sin(frequency * timer) > 0)
                    {
                        function = amplitude;
                    }
                    else
                    {
                        function = 0;
                    }
                }
                function += offset;

                position.y += function;
                IrregularitiesMover spawned = Instantiate(irregularitie, position, transform.rotation).GetComponent<IrregularitiesMover>();

                spawned.direction = Vector3.left;
                spawned.velocity = 0.1f;

                timer += 0.008f;
                yield return new WaitForSeconds(0.008f);
            }
        }
    }
}