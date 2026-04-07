using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DroneProfile {
    public string modelName;
    public float sensorWidth;
    public float sensorHeight;
    public float focalLength;
    public float vFov;
}

[CreateAssetMenu(fileName = "NewDroneDatabase", menuName = "Senzors/Drone Database")]
public class DroneDatabase : ScriptableObject {
    public List<DroneProfile> drones = new List<DroneProfile>();
}