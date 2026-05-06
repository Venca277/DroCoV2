// ============================================================
// SimpleAccordion.cs
//
// Author: Václav Sovák
// Date: 2026-05-06
//
// Accordion toggle only opens if the relevant
// data is actually available (drones connected or mission set).
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SimpleAccordion : MonoBehaviour {
    [Header("Content")]
    public GameObject contentObject;

    public void Toggle() {
        if (contentObject == null) {
            return;
        }

        bool currentState = contentObject.activeSelf;
        if (contentObject == null) {
            Debug.LogWarning("no content object");
            return;
        }

        //check if relevant data is available
        if (contentObject.transform.parent.name == "DroneListContainer") {
            if (DroneManager.Instance.Drones.Count > 0) {
                contentObject.SetActive(!currentState);
            } else {
                Toast.call.Show("No drones connected", 2.0f, false);
            }
        } else if (contentObject.transform.parent.name == "MissionsListContainer") {
            if (MissionUI.Instance != null && MissionUI.Instance.HasMission()) {
                contentObject.SetActive(!currentState);
            } else
                Toast.call.Show("No mission selected", 2.0f, false);
        }

        //rebuild layout to update the accordion size
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        if (transform.parent != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
        }
    }
}
