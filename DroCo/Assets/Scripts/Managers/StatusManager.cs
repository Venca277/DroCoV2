// ============================================================
// StatusManager.cs
//
// Author: Václav Sovák
// Date: 2026-05-05
//
// status updater for warnings and signal
// received in drone status data and displays them
// as notifications. 
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Michsky.MUIP;
using TMPro;

public class StatusManager : MonoBehaviour {

    [Header("Status Object")]
    public TMP_Text signal;
    public TMP_Text battery;
    public TMP_Text droneName;
    public TMP_Text missionName;

    public NotificationManager notifPrefab;
    public Transform notificationParent;

    //warning flags for displaying only once
    private bool warningActive = false;
    private bool warningWind = false;
    private bool warningDistance = false;
    private bool warningHeight = false;
    private bool warningIMU = false;
    private bool warningCompass = false;

    //warning types
    private const int WARNING_TYPE_WIND = 0;
    private const int WARNING_TYPE_DISTANCE = 1;
    private const int WARNING_TYPE_HEIGHT = 2;
    private const int WARNING_TYPE_IMU = 3;
    private const int WARNING_TYPE_COMPASS = 4;
    public bool MissionRunning { get; private set; } = false;

    public void HandleReceivedStatusUpdate(DroneStatusData statusData) {

        //check if we got info about the drone we display
        if (DroneManager.Instance.Drones.ContainsKey(statusData.client_id)) {
            //update signal icon in the top bar
            switch (statusData.gps.signal_level) {
                case 0:
                    signal.text = "0 %";
                    signal.color = Color.red;
                    break;
                case 1:
                    signal.text = "20 %";
                    signal.color = Color.red;
                    break;
                case 2:
                    signal.text = "40 %";
                    signal.color = Color.yellow;
                    break;
                case 3:
                    signal.text = "60 %";
                    signal.color = Color.yellow;
                    break;
                case 4:
                    signal.text = "80 %";
                    signal.color = Color.green;
                    break;
                case 5:
                    signal.text = "100 %";
                    signal.color = Color.green;
                    break;
                default:
                    signal.text = "No Signal";
                    signal.color = Color.red;
                    break;
            }

            //update name of drone or set uknown
            if (statusData.drone_model != null && statusData.drone_model != droneName.text) {
                droneName.text = statusData.drone_model;
            } else if (droneName.text == null || droneName.text == "") {
                droneName.text = "Unknown Aircraft";
            }

            //set distance
            if (statusData.gps.distance_from_home > 0) {
                missionName.text = statusData.gps.distance_from_home.ToString("0.0") + " m";
            }

            //display warnings only once
            if (statusData.warnings.strong_wind_warning && !warningWind) {
                Toast.call.Show("Strong wind warning!", 5f, true);
                setWarning(WARNING_TYPE_WIND);
            } else if (statusData.warnings.max_distance_reached && !warningDistance) {
                Toast.call.Show("Max distance warning!", 5f, true);
                setWarning(WARNING_TYPE_DISTANCE);
            } else if (statusData.warnings.max_height_reached && !warningHeight) {
                Toast.call.Show("Max height warning!", 5f, true);
                setWarning(WARNING_TYPE_HEIGHT);
            } else if (statusData.warnings.imu_preheating && !warningIMU) {
                Toast.call.Show("IMU preheating warning!", 5f, true);
                setWarning(WARNING_TYPE_IMU);
            } else if (statusData.warnings.compass_error && !warningCompass) {
                Toast.call.Show("Compass error warning!", 5f, true);
                setWarning(WARNING_TYPE_COMPASS);
            }
        } else { //uknown drone id

        }
    }

    //set warning flags to display only once
    private void setWarning(int WARNING_TYPE) {
        switch (WARNING_TYPE) {
            case WARNING_TYPE_WIND:
                warningWind = true;
                warningDistance = false;
                warningHeight = false;
                warningCompass = false;
                warningIMU = false;
                break;
            case WARNING_TYPE_DISTANCE:
                warningDistance = true;
                warningWind = false;
                warningHeight = false;
                warningCompass = false;
                warningIMU = false;
                break;
            case WARNING_TYPE_HEIGHT:
                warningHeight = true;
                warningWind = false;
                warningDistance = false;
                warningCompass = false;
                warningIMU = false;
                break;
            case WARNING_TYPE_IMU:
                warningIMU = true;
                warningWind = false;
                warningDistance = false;
                warningHeight = false;
                warningCompass = false;
                break;
            case WARNING_TYPE_COMPASS:
                warningCompass = true;
                warningWind = false;
                warningDistance = false;
                warningHeight = false;
                warningIMU = false;
                break;
            default:
                break;
        }

    }
}
