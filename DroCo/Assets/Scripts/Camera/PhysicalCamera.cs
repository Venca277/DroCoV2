using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PhysicalCamera : MonoBehaviour {

    public float f = 35.0f; // f can be arbitrary, as long as sensor_size is resized to to make ax,ay consistient

    public Camera MainCamera;

    // Use this for initialization
    private void Start() {
        //mainCamera = GetComponent<Camera>();
        ChangeCameraParam();
    }

    private void ChangeCameraParam() {
        float fx, fy, sizeX, sizeY;
        float cx, cy, shiftX, shiftY;
        int width, height;

        fx = 1476.99807f;
        fy = 1477.05696f;
        cx = 973.405537f;
        cy = 529.885756f;

        width = 1920;
        height = 1080;

        sizeX = f * width / fx;
        sizeY = f * height / fy;

        shiftX = -(cx - width / 2.0f) / width;
        shiftY = (cy - height / 2.0f) / height;

        MainCamera.sensorSize = new Vector2(sizeX, sizeY);     // in mm, mx = 1000/x, my = 1000/y
        MainCamera.focalLength = f;                            // in mm, ax = f * mx, ay = f * my
        MainCamera.lensShift = new Vector2(shiftX, shiftY);    // W/2,H/w for (0,0), 1.0 shift in full W/H in image plane

    }

    private void OnEnable() {
        ChangeCameraParam();
    }

}
