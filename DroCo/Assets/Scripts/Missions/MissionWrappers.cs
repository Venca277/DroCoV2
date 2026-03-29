using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
//using System.Windows.Input;
using Esri.GameEngine.Geometry;

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

public class ControlCommandData {
    public float pitch;
    public float roll;
    public float yaw;
    public float throttle;
    public float gimbal_pitch;
}