// ============================================================
// MissionWrappers.cs
//
// Author: Václav Sovák
// Date: 2026-04-05
//
// Plain data classes used for JSON
// serialization. Network messages, mission save files and
// waypoint data.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using Esri.GameEngine.Geometry;

//wrapper for sending mission data over network and saving/loading missions
public class NetworkWrapper {
    public string type;
    public MissionData data;
}

public class MissionSave {
    public MissionData mission;
    public BuildingInfo building;
}

public class BuildingInfo {
    public string name;
    public float minY;
    public float maxY;
    public float buildingHeight;
    public List<GpsCorner> footprint;
}

public class GpsCorner {
    public double lat;
    public double lon;
}

public class ControlCommand {
    public string type = "control_command";
    public ControlCommandData data;
}

//wrapper for virtual mission navigation
public class ControlCommandData {
    public float pitch;
    public float roll;
    public float yaw;
    public float throttle;
    public float gimbal_pitch;
}

//wrapper for each waypoint
public class WaypointData {
    public List<GameObject> tubes = new List<GameObject>();
    public int level = 0;
    public Vector3 normal;
}