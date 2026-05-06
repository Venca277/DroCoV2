// ============================================================
// DroneDatabase.cs
//
// Author:  Václav Sovák
// Date:    2026-04-26
//
// Database of drone camera profiles. Used by Settings to 
// configure parameters based on the selected drone model.
// ============================================================

using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DroneProfile {
    public string modelName;
    public float sensorWidth;   //sensor width in mm
    public float sensorHeight;  //sensor height in mm
    public float focalLength;   //focal length in mm
    public float vFov;          //vertical field of view in degrees
}

[CreateAssetMenu(fileName = "NewDroneDatabase", menuName = "Senzors/Drone Database")]
public class DroneDatabase : ScriptableObject {
    public List<DroneProfile> drones = new List<DroneProfile>();
}