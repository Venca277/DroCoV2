using System.Collections;
using System.Collections.Generic;
//using Microsoft.Unity.VisualStudio.Editor;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class StatusUpdate : Singleton<StatusUpdate> {

    [Header("Icons")]
    public Sprite noSignal;
    public Sprite signalLow;
    public Sprite signalMedium;
    public Sprite signalHigh;
    public Sprite signalVeryHigh;
    public Sprite signalExcellent;
    public Sprite drone;
    public Sprite droneActive;
    public Sprite batteryFull;
    public Sprite batteryMedium;
    public Sprite batteryLow;
    public Sprite batteryCritical;
    public Sprite warningWind;
    public Sprite warningHeight;
    public Sprite warningTemperature;
    public Sprite warningHome;
    public Sprite warningCompass;
    public Sprite statusOK;

    public void HandleStatusUpdate(DroneStatusData status) {
        Debug.Log("Status update: " + status);

        if (status == null) {
            Debug.LogWarning("Received null status data");
            return;
        }

        GameObject topbar = GameObject.Find("TopBar");
        if (topbar == null) {
            Debug.LogWarning("Could not find TopBar in the scene");
            return;
        }
        Transform left = topbar.transform.Find("Left");
        Transform stroke = topbar.transform.Find("stroke");
        if (left == null || stroke == null) {
            Debug.LogWarning("Could not find Left or stroke in TopBar");
            return;
        }

        Image signal = left.Find("signal").GetComponent<Image>();
        TMP_Text latency = left.Find("latency").GetComponent<TMP_Text>();
        if (signal == null || latency == null) {
            Debug.LogWarning("Could not find signal image or latency text in Left");
            return;
        }

        Image droneIcon = stroke.Find("drone").GetComponent<Image>();
        Image missionIcon = stroke.Find("mission").GetComponent<Image>();
        TMP_Text droneName = stroke.Find("droneText").GetComponent<TMP_Text>();
        TMP_Text missionName = stroke.Find("missionText").GetComponent<TMP_Text>();
        if (droneIcon == null || missionIcon == null || droneName == null || missionName == null) {
            Debug.LogWarning("Could not find drone or mission icons or texts in stroke");
            return;
        }
        droneIcon.preserveAspect = false;
        missionIcon.preserveAspect = false;
        signal.preserveAspect = false;


        if (status.drone_model != null && status.drone_model != "") {
            droneIcon.sprite = droneActive;
            droneIcon.rectTransform.sizeDelta = new Vector2(80, 80);
            droneName.text = status.drone_model;
            droneName.fontSize = 36;
        } else {
            droneIcon.sprite = drone;
            droneIcon.rectTransform.sizeDelta = new Vector2(80, 80);
            droneName.text = "Unknown aircraft";
            droneName.fontSize = 36;
        }

        if (status.gps != null && status.gps.signal_level >= 0 && status.gps.signal_level <= 5) {
            switch (status.gps.signal_level) {
                case 0:
                    signal.sprite = noSignal;
                    latency.text = "0 %";
                    break;
                case 1:
                    signal.sprite = signalLow;
                    latency.text = "20 %";
                    break;
                case 2:
                    signal.sprite = signalMedium;
                    latency.text = "40 %";
                    break;
                case 3:
                    signal.sprite = signalHigh;
                    latency.text = "60 %";
                    break;
                case 4:
                    signal.sprite = signalVeryHigh;
                    latency.text = "80 %";
                    break;
                case 5:
                    signal.sprite = signalExcellent;
                    latency.text = "100 %";
                    break;
            }
            signal.rectTransform.sizeDelta = new Vector2(60, 60);
        } else {
            Debug.LogWarning("Invalid GPS signal");
        }


        GameObject dronelist = GameObject.Find("DroneListContainer");
        if (dronelist == null) {
            Debug.LogWarning("Could not find DroneListContainer");
            return;
        }
        Transform header = dronelist.transform.Find("Header");
        Transform content = dronelist.transform.Find("Content");
        if (content == null || header == null) {
            Debug.LogWarning("Could not find Content in DroneListContainer");
            return;
        }
        Transform row1 = content.Find("Row1");
        Transform row1status = content.Find("Row1status");
        if (row1 == null || row1status == null) {
            Debug.LogWarning("Could not find Row1 or Row1status in Content");
            return;
        }

        Transform leftdrone = row1.Find("Left");
        TMP_Text droneText = leftdrone.Find("droneText").GetComponent<TMP_Text>();
        Image dronebarIcon = header.Find("droneIcon").GetComponent<Image>();
        Image batteryIcon = row1.Find("batteryIcon").GetComponent<Image>();
        if (droneText == null || batteryIcon == null || dronebarIcon == null) {
            Debug.LogWarning("Could not find droneText or batteryIcon or dronebarIcon in Row1");
            return;
        }

        if (status.drone_model != null && status.drone_model != "") {
            droneText.text = status.drone_model;
            droneText.fontSize = 30;
            dronebarIcon.sprite = droneActive;
        } else {
            droneText.text = "Unknown aircraft";
            droneText.fontSize = 30;
            dronebarIcon.sprite = droneActive;
        }

        if (status.battery != null) {
            if (status.battery.low_battery_warning) {
                batteryIcon.sprite = batteryCritical;
            } else if (status.battery.remaining_percent >= 75) {
                batteryIcon.sprite = batteryFull;
            } else if (status.battery.remaining_percent >= 50) {
                batteryIcon.sprite = batteryMedium;
            } else if (status.battery.remaining_percent >= 25) {
                batteryIcon.sprite = batteryLow;
            } else {
                batteryIcon.sprite = batteryCritical;
            }
        } else {
            Debug.LogWarning("Battery data is null");
            batteryIcon.sprite = batteryCritical;
        }
        batteryIcon.rectTransform.sizeDelta = new Vector2(40, 40);

        Image state1 = row1status.Find("state1").GetComponent<Image>();
        Image state2 = row1status.Find("state2").GetComponent<Image>();
        Image state3 = row1status.Find("state3").GetComponent<Image>();
        Image state4 = row1status.Find("state4").GetComponent<Image>();
        Image state5 = row1status.Find("state5").GetComponent<Image>();
        if (state1 == null || state2 == null || state3 == null || state4 == null || state5 == null) {
            Debug.LogWarning("Could not find state images in Row1status");
            return;
        }
        state1.gameObject.SetActive(false);
        state2.gameObject.SetActive(false);
        state3.gameObject.SetActive(false);
        state4.gameObject.SetActive(false);
        state5.gameObject.SetActive(false);

        if (status.warnings != null) {
            if (!status.warnings.strong_wind_warning && !status.warnings.max_height_reached && !status.warnings.max_distance_reached && !status.warnings.imu_preheating && !status.warnings.compass_error) {
                state1.sprite = statusOK;
                state1.gameObject.SetActive(true);
            } else {
                if (status.warnings.strong_wind_warning) {
                    state1.sprite = warningWind;
                    state1.gameObject.SetActive(true);
                    state1.rectTransform.sizeDelta = new Vector2(40, 40);
                }
                if (status.warnings.max_height_reached) {
                    state2.sprite = warningHeight;
                    state2.gameObject.SetActive(true);
                    state2.rectTransform.sizeDelta = new Vector2(40, 40);
                }
                if (status.warnings.imu_preheating) {
                    state3.sprite = warningTemperature;
                    state3.gameObject.SetActive(true);
                    state3.rectTransform.sizeDelta = new Vector2(40, 40);
                }
                if (status.warnings.max_distance_reached) {
                    state4.sprite = warningHome;
                    state4.gameObject.SetActive(true);
                    state4.rectTransform.sizeDelta = new Vector2(40, 40);
                }
                if (status.warnings.compass_error) {
                    state5.sprite = warningCompass;
                    state5.gameObject.SetActive(true);
                    state5.rectTransform.sizeDelta = new Vector2(40, 40);
                }
            }
        } else {
            state1.sprite = statusOK;
            state1.gameObject.SetActive(true);
        }
    }
}
