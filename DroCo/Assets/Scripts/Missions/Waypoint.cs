using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Waypoint {

    public GPS Coordinates {
        get; private set;
    }

    public double Altitude {
        get; private set;
    }

    public float Speed {
        get; set;
    } = 5.0f;

    public float Heading {
        get; set;
    } = 0.0f;

    public float GimbalPitch {
        get; set;
    } = -45.0f;

    public float GimbalYaw {
        get; set;
    } = 0.0f;

    private GameObject objectVisual;

    public Waypoint(GPS coordinates, double altitude) {
        Coordinates = coordinates;
        Altitude = altitude;
    }

    public Waypoint(GPS coordinates, double altitude, float speed, float heading, float gimbalPitch, float gimbalYaw) {
        Coordinates = coordinates;
        Altitude = altitude;
        Speed = speed;
        Heading = heading;
        GimbalPitch = gimbalPitch;
        GimbalYaw = gimbalYaw;
    }

    public void SetVisual(GameObject visual) {
        objectVisual = visual;
    }

    public void DestroyVisual() {
        GameObject.Destroy(objectVisual);
    }
}
