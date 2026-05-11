// ============================================================
// DroneCollisionAlert.cs
//
// Author:  Václav Sovák
// Date:    2026-04-25
//
// Checks drone proximity to buildings and shows a warning when 
// too close. Warning distance or critical distance.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneCollisionAlert : MonoBehaviour {
    public float warnDist = 1f;
    public float dangerDist = 0.5f;
    public float check = 1f;
    public bool alertEnabled = false;
    int building = LayerMask.GetMask("Buildings");

    void Start() {
        //repeat check in interval
        InvokeRepeating(nameof(CheckProximity), 1f, check);
    }

    //notify user of nearby buildings in warning or danger distance
    void CheckProximity() {
        Vector3 dronePos = DroneManager.Instance?.GetFirstDronePosition() ?? Vector3.zero;
        if (dronePos == Vector3.zero)
            return;

        //check for nearby buildings in warning distance
        Collider[] hits = Physics.OverlapSphere(dronePos, warnDist, building);
        if (hits.Length == 0)
            return;

        //find closest hit
        float closest = float.MaxValue;
        foreach (Collider col in hits) {
            float dist = Vector3.Distance(dronePos, col.ClosestPoint(dronePos));
            if (dist < closest)
                closest = dist;
        }
        //turn of warnings
        if (!alertEnabled)
            return;

        if (closest < dangerDist) {
            Toast.call.Show($"DANGER: building {closest:F0}m away!", 1f, true);
        } else {
            Toast.call.Show($"Warning: building {closest:F0}m away", 1f, false);
        }
    }
}
