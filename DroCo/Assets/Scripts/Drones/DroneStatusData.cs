using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using System;

[Serializable]
public class DroneStatusData {
    public string client_id;
    public long timestamp;

    public string drone_name;
    public GPSData gps;
    public BatteryData battery;
    public OrientationData orientation;
    public VelocityData velocity;
    public WarningsData warnings;
}

[Serializable]
public class GPSData {
    public float latitude;
    public float longitude;
    public float altitude;
    public int satellite_count;
    public int signal_level;
    public string signal_name;
    public float distance_from_home;
}

[Serializable]
public class BatteryData {
    public float remaining_percent;
    public float voltage;
    public float current;
    public float temperature;
    public float remaining_time;
    public bool low_battery_warning;
}

[Serializable]
public class OrientationData {
    public float pitch;
    public float roll;
    public float yaw;
    public float compass;
}

[Serializable]
public class VelocityData {
    public float x;
    public float y;
    public float z;
    public float horizontal_velocity;
}

[Serializable]
public class WarningsData {
    public bool strong_wind_warning;
    public bool max_height_reached;
    public bool max_distance_reached;
    public bool imu_preheating;
    public bool compass_error;
}