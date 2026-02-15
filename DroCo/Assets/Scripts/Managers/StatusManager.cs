using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Michsky.MUIP;
using TMPro;

public class StatusManager : MonoBehaviour {

    [Header("Status Object")]
    [SerializeField] private TMP_Text signal;
    [SerializeField] private TMP_Text battery;
    [SerializeField] private TMP_Text droneName;
    [SerializeField] private TMP_Text missionName;


    [SerializeField] private NotificationManager notifPrefab;
    [SerializeField] private Transform notificationParent;

    private bool warningActive = false;
    private bool warningWind = false;
    private bool warningDistance = false;
    private bool warningHeight = false;
    private bool warningIMU = false;
    private bool warningCompass = false;

    private const int WARNING_TYPE_WIND = 0;
    private const int WARNING_TYPE_DISTANCE = 1;
    private const int WARNING_TYPE_HEIGHT = 2;
    private const int WARNING_TYPE_IMU = 3;
    private const int WARNING_TYPE_COMPASS = 4;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void HandleReceivedStatusUpdate(DroneStatusData statusData) {

        //check if we got info about the drone we display
        if (DroneManager.Instance.Drones.ContainsKey(statusData.client_id)) {
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

            if (statusData.drone_model != null && statusData.drone_model != droneName.text) {
                droneName.text = statusData.drone_model;
            } else if (droneName.text == null || droneName.text == "") {
                droneName.text = "Unknown Aircraft";
            }

            if (statusData.gps.distance_from_home > 0) {
                missionName.text = statusData.gps.distance_from_home.ToString("0.0") + " m";
            }

            if (statusData.warnings.strong_wind_warning && !warningWind) {
                ShowWarning("Strong Wind Warning", "The drone is experiencing strong winds.");
                setWarning(WARNING_TYPE_WIND);
            } else if (statusData.warnings.max_distance_reached && !warningDistance) {
                ShowWarning("Max Distance Warning", "The drone has reached its maximum distance from the home point.");
                setWarning(WARNING_TYPE_DISTANCE);
            } else if (statusData.warnings.max_height_reached && !warningHeight) {
                ShowWarning("Max Height Warning", "The drone has reached its maximum height.");
                setWarning(WARNING_TYPE_HEIGHT);
            } else if (statusData.warnings.imu_preheating && !warningIMU) {
                ShowWarning("IMU Preheating", "The drone's IMU is preheating. Please wait.");
                setWarning(WARNING_TYPE_IMU);
            } else if (statusData.warnings.compass_error && !warningCompass) {
                ShowWarning("Compass Error", "The drone is having a compass error.");
                setWarning(WARNING_TYPE_COMPASS);
            }
        } else { //prisla data s neznamym drone ID -> pozadame server o novy seznam dronu

        }
    }

    public void ShowWarning(string title, string message) {
        NotificationManager notif = Instantiate(this.notifPrefab, notificationParent);

        notif.title = title;
        notif.description = message;
        notif.enableTimer = true;
        notif.timer = 3f;

        notif.UpdateUI();
        notif.Open();
    }

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
