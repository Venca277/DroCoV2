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

    private GameObject objectVisual;

    public Waypoint(GPS coordinates, double altitude) {
        Coordinates = coordinates;
        Altitude = altitude;
    }

    public void SetVisual(GameObject visual) {
        objectVisual = visual;
    }

    public void DestroyVisual() {
        GameObject.Destroy(objectVisual);
    }
}
