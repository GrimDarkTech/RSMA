using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    public PrefabData prefabData;

    public List<RSMADrone> drones;

    public List<GameObject> robots;
    public List<RSMAGPIO> GPIOs;

    public List<GameObject> walls;

    public List<GameObject> markers;

    public List<TransformWriter> writers;

    private void Start()
    {
        CommandHandler.objectManager = this;
    }

    public void InstantiateRobot(string name, Vector3 position, Vector3 rotation)
    {
        PrefabInfo prefabInfo = prefabData.robots.Find(x => x.name == name);
        if (prefabInfo != null)
        {
            var robot = Instantiate(prefabInfo.prefab, position, Quaternion.Euler(rotation));

            robots.Add(robot);

            RSMAGPIO gpio = robot.GetComponentInChildren<RSMAGPIO>();

            GPIOs.Add(gpio);

            writers.Add(robot.GetComponent<TransformWriter>());
        }
    }

    public void GPIOWrite(int id, string portName, string pinName, float value)
    {
        RSMAGPIO gpio = GPIOs[id];
        
        gpio.WritePin(portName, pinName, value);    
    }
    public float GPIORead(int id, string portName, string pinName)
    {
        RSMAGPIO gpio = GPIOs[id];

        return gpio.ReadPin(portName, pinName);
    }

    public void InstantiateWall(Vector3 start, Vector3 end, float height, float width)
    {
        PrefabInfo prefabInfo = prefabData.walls.Find(x => x.name == "Wall");
        if (prefabInfo != null)
        {
            var wall = Instantiate(prefabInfo.prefab);

            wall.transform.localScale = new Vector3(width, height, Vector3.Distance(end, start));
            wall.transform.rotation = Quaternion.LookRotation(end - start);
            wall.transform.position = start;

            walls.Add(wall);
        }
    }

    public void InstantiateMarker(string name, Vector3 position)
    {
        PrefabInfo prefabInfo = prefabData.markers.Find(x => x.name == name);
        if (prefabInfo != null)
        {
            markers.Add(Instantiate(prefabInfo.prefab, position, prefabInfo.prefab.transform.rotation));
        }
    }
    public void InstantiateDrone(Vector3 position)
    {
        PrefabInfo prefabInfo = prefabData.drones.Find(x => x.name == "RSMADrone");
        if (prefabInfo != null)
        {
            var drone = Instantiate(prefabInfo.prefab, position, prefabInfo.prefab.transform.rotation);
            drones.Add(drone.GetComponent<RSMADrone>());
        }
    }

    public void DroneSetAcceleration(int id, Vector3 acceleration)
    {
        RSMADrone drone = drones[id];

        drone.targetAcceleration = acceleration;
    }
    public void DroneCamera(int id, Vector3 rotation, float smooth)
    {
        RSMADrone drone = drones[id];

        drone.cameraRotation = rotation;
        drone.cameraTurnSmoothness = smooth;
    }
    public void DroneSwitch(int id)
    {
        RSMADrone drone = drones[id];

        if(drone != null)
        {
            foreach (RSMADrone _drone in drones)
            {
                _drone.SetCamera(false);
            }

            drone.SetCamera(true);
        }
    }
    public void DroneManualControl(int id, bool mode)
    {
        RSMADrone drone = drones[id];

        drone.gameObject.GetComponent<DroneInput>().enabled = mode;
    }
    public void DroneMoveToPosition(int id, Vector3 targetPosition, float kp, float ki, float kd)
    {
        RSMADrone drone = drones[id];

        drone.MoveToPosition(targetPosition, kp, ki, kd);
    }

    public void WriterStart(int id)
    {
        writers[id].Write();
    }

    public void WriterStop(int id)
    {
        writers[id].Stop();
    }

    [Serializable]
    public class PrefabInfo
    {
        public string name;
        public GameObject prefab;
    }
}

