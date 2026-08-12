using UnityEngine;
using UnityEngine.UI;

namespace RSMA.ObjectManager.UI
{
    public class FollowSettingsUI : MonoBehaviour
    {
        public CameraFollower cameraFollower;
        public Font font;

        private void Start()
        {
            if (cameraFollower == null)
                cameraFollower = Camera.main.GetComponent<CameraFollower>();
        }

        // Пример метода подстройки дистанции Offset Y / Z через UI
        public void SetOffsetY(float value)
        {
            if (cameraFollower != null)
                cameraFollower.offset.y = value;
        }

        public void SetOffsetZ(float value)
        {
            if (cameraFollower != null)
                cameraFollower.offset.z = value;
        }

        public void SetRotationX(float value)
        {
            if (cameraFollower != null)
                cameraFollower.rotationOffset.x = value;
        }
    }
}