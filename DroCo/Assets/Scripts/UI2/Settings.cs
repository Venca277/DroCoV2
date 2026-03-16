using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Settings : MonoBehaviour {

    [Header("UI")]
    public MissionGenerator generator;
    public TMP_Dropdown droneType;
    public TMP_Dropdown controllMode;
    public TMP_Dropdown preloadOSM;
    public DroneDatabase database;


    private List<string> drones = new List<string>();
    public bool isWaypointMission = false;
    public string range = "";

    // Start is called before the first frame update
    void Start() {
        if (droneType != null) {
            droneType.ClearOptions();
            foreach (DroneProfile drone in database.drones) {
                drones.Add(drone.modelName);
            }
            droneType.AddOptions(drones);
            droneType.value = 0;
            droneType.onValueChanged.AddListener(OnDroneTypeChanged);
        }
        if (controllMode != null) {
            controllMode.ClearOptions();
            controllMode.AddOptions(new List<string> { "Virtual Sticks", "Waypoint Mission" });
            controllMode.value = 0;
            controllMode.onValueChanged.AddListener(OnControlModeChanged);
        }

        if (preloadOSM != null) {
            preloadOSM.ClearOptions();
            preloadOSM.AddOptions(new List<string> { "Do not preload", "range 100m", "range 200m", "range 300m", "range 400m", "range 500m" });
            preloadOSM.value = 1;
            preloadOSM.onValueChanged.AddListener(OnPreloadOSMChanged);
        }
    }

    // Update is called once per frame
    void Update() {

    }

    private void OnDroneTypeChanged(int index) {
        foreach (DroneProfile drone in database.drones) {
            if (drone.modelName == drones[index]) {
                generator.senzHeight = drone.sensorHeight;
                generator.senzWidth = drone.sensorWidth;
                generator.focalLength = drone.focalLength;
                break;
            }
        }
    }

    private void OnControlModeChanged(int index) {
        if (index == 0) {
            isWaypointMission = false;
        } else {
            isWaypointMission = true;
        }
    }

    private void OnPreloadOSMChanged(int index) {
        switch (index) {
            case 0:
                range = "none";
                break;
            case 1:
                range = "100";
                break;
            case 2:
                range = "200";
                break;
            case 3:
                range = "300";
                break;
            case 4:
                range = "400";
                break;
            case 5:
                range = "500";
                break;
        }
    }

    public int getRange() {
        if (range == "none")
            return 0;
        if (int.TryParse(range, out int r)) {
            return r;
        }
        return 0;
    }
}
