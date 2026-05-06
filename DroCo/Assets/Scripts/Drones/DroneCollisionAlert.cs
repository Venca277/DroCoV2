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
    public float warnDist = 2f;
    public float dangerDist = 1f;
    public float check = 0.5f;

    int building = LayerMask.GetMask("Buildings");

    void Start() {
        //repeat check in interval
        InvokeRepeating(nameof(CheckProximity), 1f, check);
    }

    //notify user of nearby buildings in warning or danger distance
    void CheckProximity() {
        //check for nearby buildings in warning distance
        Collider[] hits = Physics.OverlapSphere(transform.position, warnDist, building);
        if (hits.Length == 0)
            return;

        //find closest hit
        float closest = float.MaxValue;
        foreach (Collider col in hits) {
            float dist = Vector3.Distance(transform.position, col.ClosestPoint(transform.position));
            if (dist < closest)
                closest = dist;
        }

        if (closest < dangerDist) {
            Toast.call.Show($"DANGER: building {closest:F0}m away!", 1f, true);
        } else {
            Toast.call.Show($"Warning: building {closest:F0}m away", 1f, false);
        }
    }
}
