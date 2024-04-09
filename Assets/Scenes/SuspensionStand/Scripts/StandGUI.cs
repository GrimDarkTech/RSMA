using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace SuspensionTest
{
    public class StandGUI : MonoBehaviour
    {
        public IrregularitiesSpawner spawner;

        public InputField amplitude;
        public InputField frequency;
        public InputField offset;

        public void SetAmplitude()
        {
            spawner.amplitude = float.Parse(amplitude.text, CultureInfo.InvariantCulture);
        }
        public void SetFrequency()
        {
            spawner.frequency = float.Parse(frequency.text, CultureInfo.InvariantCulture);
        }
        public void SetOffset()
        {
            spawner.offset = float.Parse(offset.text, CultureInfo.InvariantCulture);
        }
    }
}