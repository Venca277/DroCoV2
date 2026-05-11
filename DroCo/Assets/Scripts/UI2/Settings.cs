// ============================================================
// Settings.cs
//
// Author: Václav Sovák
// Date: 2026-05-06
//
// UI settings wires dropdowns and toggles to the
// rest of the system.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Settings : MonoBehaviour {

    [Header("UI")]
    public MissionGenerator generator;
    public BuildingFetcher buildingFetcher;
    public MissionController missionController;
    public BuildingHover buildingHover;
    public GameManager gameManager;
    public Navigator navigator;
    public DroneCollisionAlert collisionAlert;
    public TMP_Dropdown droneType;
    public TMP_Dropdown controllMode;
    public TMP_Dropdown preloadOSM;
    public DroneDatabase database;
    public Toggle showGhost;
    public Toggle autoConnect;
    public Toggle reverseOrder;
    public Toggle liftBuildings;
    public Toggle userCenter;
    public Toggle showWarnings;

    private List<string> drones = new List<string>();
    public bool isWaypointMission = false;
    public string range = "";

    void Start() {
        //initializes default values and adds listeners
        if (droneType != null) {
            droneType.ClearOptions();
            foreach (DroneProfile drone in database.drones) {
                drones.Add(drone.modelName);
            }
            droneType.AddOptions(drones);
            droneType.onValueChanged.AddListener(OnDroneTypeChanged);
            droneType.value = 0;
        }
        if (controllMode != null) {
            controllMode.ClearOptions();
            controllMode.AddOptions(new List<string> { "Virtual Sticks", "Waypoint Mission" });
            controllMode.onValueChanged.AddListener(OnControlModeChanged);
            controllMode.value = 0;
        }

        if (preloadOSM != null) {
            preloadOSM.ClearOptions();
            preloadOSM.AddOptions(new List<string> { "Do not preload", "range 100m", "range 200m", "range 300m", "range 400m", "range 500m" });
            preloadOSM.onValueChanged.AddListener(OnPreloadOSMChanged);
            preloadOSM.value = 1;
        }
        if (showGhost != null) {
            showGhost.onValueChanged.AddListener(OnShowGhostChanged);
        }
        if (autoConnect != null) {
            autoConnect.onValueChanged.AddListener(OnAutoConnectChanged);
        }
        if (reverseOrder != null) {
            reverseOrder.onValueChanged.AddListener(OnReverseOrderChanged);
        }
        if (liftBuildings != null) {
            liftBuildings.onValueChanged.AddListener(OnLiftBuildingsChanged);
        }
        if (userCenter != null) {
            userCenter.onValueChanged.AddListener(OnUserCenterChanged);
        }
        if (showWarnings != null) {
            showWarnings.onValueChanged.AddListener(OnShowWarningsChanged);
        }
    }

    //apply parameters of the selected drone profile
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

    //switch control mode
    private void OnControlModeChanged(int index) {
        if (index == 0) {
            isWaypointMission = false;
        } else {
            isWaypointMission = true;
        }
    }

    //change OSM preload range
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

    //toggle building visibility
    private void OnShowGhostChanged(bool value) {
        buildingFetcher.SetShowGhost(value);
    }

    //toggle auto connect trajectory
    public void OnAutoConnectChanged(bool value) {
        missionController.autoConnect = value;
    }

    //get OSM preload range in meters
    public int GetRange() {
        if (range == "none")
            return 0;
        if (int.TryParse(range, out int r)) {
            return r;
        }
        return 0;
    }

    private void OnReverseOrderChanged(bool value) {
        navigator.reverseOrder = value;
    }

    private void OnLiftBuildingsChanged(bool value) {
        buildingHover.lift = value;
    }

    private void OnUserCenterChanged(bool value) {
        gameManager.userCenter = value;
    }

    private void OnShowWarningsChanged(bool value) {
        collisionAlert.alertEnabled = value;
    }
}
