using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PrefabData", menuName = "RSMA/ObjectManager Prefab Data")]
public class PrefabData : ScriptableObject
{
    public List<ObjectManager.PrefabInfo> drones;

    public List<ObjectManager.PrefabInfo> robots;

    public List<ObjectManager.PrefabInfo> walls;

    public List<ObjectManager.PrefabInfo> markers;
}