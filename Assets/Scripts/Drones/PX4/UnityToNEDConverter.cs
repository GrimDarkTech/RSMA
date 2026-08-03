using System;
using UnityEngine;

namespace RSMA.PX4
{
    public static class UnityToNEDConverter
    {
        public static Vector3 PositionToNED(Vector3 unityPos)
        {
            return new Vector3(unityPos.z, unityPos.x, -unityPos.y);
        }

        public static Vector3 VelocityToNED(Vector3 unityVel)
        {
            return new Vector3(unityVel.z, unityVel.x, -unityVel.y);
        }
        public static Vector3 AngularVelocityToNED(Vector3 unityAngularVel)
        {
            return new Vector3(
                -unityAngularVel.z, 
                -unityAngularVel.x, 
                unityAngularVel.y);
        }

        public static Vector3 BodyUnityToBodyFRD(Vector3 localUnityVec)
        {
            return new Vector3(-localUnityVec.z, localUnityVec.x, -localUnityVec.y);
        }

        public static Vector3 AngularVelocityToBodyFRD(Vector3 localAngularVelUnity)
        {
            return new Vector3(
                -localAngularVelUnity.z, 
                -localAngularVelUnity.x, 
                localAngularVelUnity.y);
        }

        public static Quaternion RotationToNED(Quaternion unityRot)
        {
            return new Quaternion(
                -unityRot.z,
                -unityRot.x,
                -unityRot.y,
                unityRot.w
            );
        }
        public static (double lat, double lon) NEDToLatLon(Vector2 nedPosNorthEast, double homeLat, double homeLon)
        {
            double latRad = homeLat * Math.PI / 180.0;
            double lat = homeLat + (nedPosNorthEast.x / 111320.0);
            double lon = homeLon + (nedPosNorthEast.y / (111320.0 * Math.Cos(latRad)));
            return (lat, lon);
        }
        public static (double lat, double lon) UnityPosToLatLon(Vector3 unityLocalPos, double homeLat, double homeLon)
        {
            Vector3 nedPos = PositionToNED(unityLocalPos);
            return NEDToLatLon(new Vector2(nedPos.x, nedPos.y), homeLat, homeLon);
        }
    }
}