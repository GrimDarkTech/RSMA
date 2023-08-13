using System.Runtime.InteropServices;
using UnityEngine;
[RequireComponent(typeof(Rigidbody))]

public class RSMARobotOrigin : MonoBehaviour
{
    [DllImport("__Internal")]
    public static extern void SayHello();
}
