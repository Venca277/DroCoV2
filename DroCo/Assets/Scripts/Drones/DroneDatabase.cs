using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DroneProfile {
    public string modelName;        // Ze sloupce "Model dronu" (např. "DJI Mini 2")
    public float sensorWidth;       // Ze sloupce "Šířka senzoru" (např. 6.17)
    public float sensorHeight;      // Ze sloupce "Výška senzoru" (např. 4.55)
    public float focalLength;       // Ze sloupce "Skutečné ohnisko (EXIF)" (např. 4.49)
    public float vFov;
}

[CreateAssetMenu(fileName = "NewDroneDatabase", menuName = "Senzors/Drone Database")]
public class DroneDatabase : ScriptableObject {
    //all drones list
    public List<DroneProfile> drones = new List<DroneProfile>();
}