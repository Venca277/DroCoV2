// ============================================================
// WaypointSelect.cs
//
// Author: Václav Sovák
// Date: 2026-05-05
//
// visual selection of a waypoint,
// sets the waypoint color to its
// original color and the selected highlight color.
// ============================================================


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaypointSelect : MonoBehaviour {

    private Renderer rend;      //renderer of the waypoint
    public Color originalColor; //original color of the waypoint

    [Header("Settings")]
    public Color selectedColor = new Color(0.2f, 0.8f, 0.2f, 1f); //color when waypoint is selected

    public void Awake() {
        rend = GetComponent<Renderer>();
        if (rend != null) {
            originalColor = new Color(1f, 0.84f, 0f, 1f); //default gold
            rend.material.color = originalColor;
        }
    }

    //set the color of the waypoint to selected or original
    public void Select(bool selected) {
        if (rend != null) {
            rend.material.color = selected ? selectedColor : originalColor;
        }
    }

}