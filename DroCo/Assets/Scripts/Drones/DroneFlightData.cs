using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GPS {
    public double latitude;
    public double longitude;

    public override string ToString() {
        return $"{{latitude:{latitude}, longitude:{longitude}}}";
    }
}

[Serializable]
public class AircraftOrientation {
    public double pitch;
    public double roll;
    public double yaw;
    public double compass;
    
    public override string ToString() {
        return $"{{pitch:{pitch}, roll:{roll}, yaw:{yaw}, compass:{compass}}}";
    }
}

[Serializable]
public class AircraftVelocity {
    public double velocity_x;
    public double velocity_y;
    public double velocity_z;

    public override string ToString() {
        return $"{{x:{velocity_x}, y:{velocity_y}, z:{velocity_z}}}";
    }
}

[Serializable]
public class GimbalOrientation {
    public double pitch;
    public double roll;
    public double yaw;
    public double yaw_relative;

    public override string ToString() {
        return $"{{pitch:{pitch}, roll:{roll}, yaw:{yaw}, yaw_relative:{yaw_relative}}}";
    }
}

[Serializable]
public class DroneFlightData {
    public string client_id;
    public double altitude;
    public GPS gps;
    public AircraftOrientation aircraft_orientation;
    public AircraftVelocity aircraft_velocity;
    public GimbalOrientation gimbal_orientation;
    public string timestamp;
    public string frame;

    public DroneFlightData() {
        client_id = "unset";
        altitude = 0;
    }

    public override string ToString() {
        return $"{{client_id:{client_id}, altitude:{altitude}, gps:{gps}, aircraft_orientation:{aircraft_orientation}, gimbal_orientation:{gimbal_orientation}, timestamp:{timestamp}}}";
    }

    //public void SetData(double height, double latitude, double longitute, double pitch, double roll, double yaw, double compass) {
    //    Altitude = height;
    //    Latitude = latitude;
    //    Longitude = longitute;
    //    Pitch = pitch;
    //    Roll = roll;
    //    Yaw = yaw;
    //    Compass = compass;
    //}
}
