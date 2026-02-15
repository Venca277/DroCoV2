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

        if (status.gps.signal_level >= 0 && status.gps.signal_level <= 5) {
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
    }

}
