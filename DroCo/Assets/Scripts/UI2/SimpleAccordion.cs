using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SimpleAccordion : MonoBehaviour {
    [Header("Content")]
    public GameObject contentObject;

    public void Toggle() {
        bool currentState = contentObject.activeSelf;
        if (contentObject == null) {
            Debug.LogWarning("Content object is not assigned in SimpleAccordion");
            return;
        }

        if (contentObject.transform.parent.name == "DroneListContainer") {
            if (DroneManager.Instance.Drones.Count > 0) {
                contentObject.SetActive(!currentState);
            } else {
                Toast.call.Show("No drones connected", 2.0f, false);
            }
        } else if (contentObject.transform.parent.name == "MissionsListContainer") {
            MissionGenerator missionGenerator = FindObjectOfType<MissionGenerator>();
            if (missionGenerator == null) {
                return;
            }
            if (missionGenerator.HasMission())
                contentObject.SetActive(!currentState);
            else
                Toast.call.Show("No mission selected", 2.0f, false);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);

        if (transform.parent != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
        }
    }
}
