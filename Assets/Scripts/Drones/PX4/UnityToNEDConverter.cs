using UnityEngine;

namespace RSMA.PX4 
{
    public static class UnityToNEDConverter
    {
        public static Vector3 PositionToNED(Vector3 unityPos)
        {
            return new Vector3(unityPos.z, unityPos.x, -unityPos.y);
        }

        public static Quaternion RotationToNED(Quaternion unityRot)
        {
            // Стандартное строгое преобразование из Unity (LHS) в NED (RHS):
            // Swapping Z/X and reversing Y axis logic for Quaternions
            return new Quaternion(
                unityRot.z,
                unityRot.x,
                -unityRot.y,
                -unityRot.w
            );
        }


        /// <summary>
        /// Преобразование скорости из Unity (X-East, Y-Up, Z-North) в NED (North, East, Down)
        /// </summary>
        public static Vector3 VelocityToNED(Vector3 unityVel)
        {
            // Аналогично позиции
            return new Vector3(unityVel.z, unityVel.x, -unityVel.y);
        }

        /// <summary>
        /// Преобразование угловой скорости из Unity (X-East, Y-Up, Z-North) в NED (North, East, Down)
        /// </summary>
        public static Vector3 AngularVelocityToNED(Vector3 unityAngularVel)
        {
            // Unity: (X, Y, Z) → NED: (Z, X, -Y)
            return new Vector3(unityAngularVel.z, unityAngularVel.x, -unityAngularVel.y);
        }

        public static Vector3 AngularVelocityToBodyFRD(Vector3 localAngularVelUnity)
        {
            // В Unity local: X-Right, Y-Up, Z-Forward
            // В PX4 FRD: X-Forward, Y-Right, Z-Down
            // Учитываем инверсию направления вращения (LHS vs RHS):
            return new Vector3(
                -localAngularVelUnity.z, // Roll speed  (вокруг Forward)
                localAngularVelUnity.x,  // Pitch speed (вокруг Right)
                -localAngularVelUnity.y  // Yaw speed   (вокруг Down)
            );
        }
    }
}
