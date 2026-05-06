// ============================================================
// DroneStatusData.cs
//
// Author:  Václav Sovák
// Date:    2026-03-20
//
// Serializable data for drone status messages received
// from WebSocket.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using System;

[Serializable]
public class DroneStatusData {
    public string client_id;
    public long timestamp;
    public string drone_model;
    public GPSData gps;                 //gps state of the drone
    public BatteryData battery;         //battery state of the drone
    public OrientationData orientation; //orientation of the drone
    public VelocityData velocity;       //velocity of the drone
    public WarningsData warnings;       //warnings and alerts from the drone
}

[Serializable]
public class GPSData {
    public float latitude;
    public float longitude;
    public float altitude;              //altitude above sea level in meters
    public int satellite_count;         //number of used satellites
    public int signal_level;            //signal strength level 0-5
    public string signal_name;          //textual description of signal strength
    public float distance_from_home;    //distance from home point in meters
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
    public float pitch;     //nose up/down
    public float roll;      //left/right tilt
    public float yaw;       //heading direction
    public float compass;   //compass heading in degrees
}

[Serializable]
public class VelocityData {
    public float x;
    public float y;
    public float z;
    public float horizontal_velocity; //combined horizontal speed
}

[Serializable]
public class WarningsData {
    public bool strong_wind_warning;
    public bool max_height_reached;
    public bool max_distance_reached;
    public bool imu_preheating;
    public bool compass_error;
}