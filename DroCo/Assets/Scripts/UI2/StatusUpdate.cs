// ============================================================
// StatusUpdate.cs
//
// Author: Václav Sovák
// Date: 2026-05-06
//
// Updates the status UI provides signal, battery,
// drone name, altitude and warning icons from live DroneStatusData.
// ============================================================

using System.Collections;
using System.Collections.Generic;
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

    [Header("UI")]
    public Image signal;
    public TMP_Text latency;
    public TMP_Text altitude;
    public Image droneIcon;
    public Image missionIcon;
    public TMP_Text droneName;
    public Image dronebarIcon;
    public TMP_Text droneText;
    public Image batteryIcon;
    public Image state1;
    public Image state2;
    public Image state3;
    public Image state4;
    public Image state5;

    private Color active = new Color(0.7f, 0.97f, 0.78f);
    //updates UI status on every status update message 
    public void HandleStatusUpdate(DroneStatusData status) {

        if (droneIcon != null)
            droneIcon.preserveAspect = false;
        if (missionIcon != null)
            missionIcon.preserveAspect = false;
        if (signal != null)
            signal.preserveAspect = false;

        //update drone icon and name in dronelist
        if (status.drone_model != null && status.drone_model != "" && droneIcon != null && droneName != null) {
            droneIcon.sprite = droneActive;
            droneIcon.rectTransform.sizeDelta = new Vector2(80, 80);
            SetDroneActive(status.drone_model);
        } else if (droneIcon != null && droneName != null) {
            droneIcon.sprite = drone;
            droneIcon.rectTransform.sizeDelta = new Vector2(80, 80);
            SetDroneActive("Unknown aircraft");
        }

        //update signal strength and latency
        if (status.gps != null && status.gps.signal_level >= 0 && status.gps.signal_level <= 5 && signal != null && latency != null) {
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

        //update drone name in dronebar and drone icon
        if (status.drone_model != null && status.drone_model != "" && droneText != null && dronebarIcon != null) {
            SetDroneActive(status.drone_model);
            dronebarIcon.sprite = droneActive;
        } else if (droneText != null && dronebarIcon != null) {
            ResetDroneUI();
            dronebarIcon.sprite = droneActive;
        }

        //update battery icon in dronelist
        if (status.battery != null && batteryIcon != null) {
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
            if (batteryIcon != null) {
                batteryIcon.sprite = batteryCritical;
            }
        }
        if (batteryIcon != null) {
            batteryIcon.rectTransform.sizeDelta = new Vector2(40, 40);
        }

        //update warning icons in dronelist
        if (state1 != null)
            state1.gameObject.SetActive(false);
        if (state2 != null)
            state2.gameObject.SetActive(false);
        if (state3 != null)
            state3.gameObject.SetActive(false);
        if (state4 != null)
            state4.gameObject.SetActive(false);
        if (state5 != null)
            state5.gameObject.SetActive(false);
        if (status.warnings != null && state1 != null && state2 != null && state3 != null && state4 != null && state5 != null) {
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
            if (state1 == null)
                return;
            state1.sprite = statusOK;
            state1.gameObject.SetActive(true);
        }

        //update altitude text
        if (altitude != null) {
            if (status.gps != null) {
                altitude.text = "( " + status.gps.altitude.ToString("F1") + " m )";
            } else {
                altitude.text = "N/A";
            }
        }
    }

    //set drone name and icon to active
    public void SetDroneActive(string model) {
        if (droneName != null) {
            droneName.text = model;
            droneName.color = active;
            droneIcon.sprite = droneActive;
        }
        if (droneText != null) {
            droneText.text = model;
            droneText.color = active;
        }
    }
    //reset drone name and icon to default
    public void ResetDroneUI() {
        if (droneName != null) {
            droneName.text = "No drone";
            droneName.color = Color.white;
            droneIcon.sprite = drone;
        }
        if (droneText != null) {
            droneText.text = "No drone";
            droneText.color = Color.white;
        }
        if (latency != null)
            latency.text = "0.0 ms";
        if (altitude != null)
            altitude.text = "( 0.0 m )";
        if (batteryIcon != null)
            batteryIcon.sprite = batteryFull;

        if (signal != null)
            signal.sprite = signalExcellent;
        if (dronebarIcon != null)
            dronebarIcon.sprite = drone;

        if (state1 != null)
            state1.gameObject.SetActive(false);
        if (state2 != null)
            state2.gameObject.SetActive(false);
        if (state3 != null)
            state3.gameObject.SetActive(false);
        if (state4 != null)
            state4.gameObject.SetActive(false);
        if (state5 != null)
            state5.gameObject.SetActive(false);
    }
}
