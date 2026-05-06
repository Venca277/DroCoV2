// ============================================================
// UIGizmo.cs
//
// Author: Václav Sovák
// Date: 2026-05-06
//
// UI drag on arrows. Each arrow handles
// one axis. Dragging moves all selected waypoints via
// MissionEditor.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.VisualScripting;

public class UIGizmo : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler {
    [Header("Waypoint")]
    public Transform selectedWaypoint;

    [Header("Axis")]
    public Vector3 axisToMove;

    [Header("Icons")]
    public Sprite icon;
    public Sprite iconpressed;
    public Image arrowImage;

    [Header("Moveplus")]
    public float moveplus = 0.05f;

    [Header("Panel")]
    public GameObject panel;
    private static List<UIGizmo> gizmos = new List<UIGizmo>();

    [Header("MissionEditor")]
    public GameObject missionEditor;

    private MissionEditor editor;
    private Camera maincam;
    void Awake() {
        gizmos.Add(this);
        arrowImage = GetComponent<Image>();
        maincam = Camera.main;
        editor = missionEditor.GetComponent<MissionEditor>();
    }

    void OnDestroy() {
        gizmos.Remove(this);
    }

    //mouse delta moves the selected waypoints along the clicked axis
    public void OnDrag(PointerEventData eventData) {
        if (selectedWaypoint == null)
            return;

        float mouseMovement = 0f;
        //determine which axis to move based on the arrow clicked
        if (Mathf.Abs(axisToMove.x) > 0) {
            mouseMovement = eventData.delta.x;
        } else if (Mathf.Abs(axisToMove.y) > 0) {
            mouseMovement = eventData.delta.y;
        } else if (Mathf.Abs(axisToMove.z) > 0) {
            mouseMovement = eventData.delta.y;
        }

        Vector3 newPos = axisToMove * (mouseMovement * moveplus);
        editor.MoveWaypoints(newPos);
    }

    //set the selected waypoint and show the panel
    public static void SetSelectedWaypoint(Transform wp) {
        foreach (var gizmo in gizmos) {
            gizmo.selectedWaypoint = wp;

            if (gizmo.panel != null) {
                if (wp != null) {
                    gizmo.panel.SetActive(true);
                } else {
                    gizmo.panel.SetActive(false);
                }
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (arrowImage != null && iconpressed != null) {
            arrowImage.sprite = iconpressed;
        }
        //disable camera control while dragging
        if (maincam != null) {
            maincam.GetComponent<ArcGISCameraControllerTouch>().enabled = false;
            Debug.Log("Disabled camera control");
        }
        editor.dragUI = true;
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (arrowImage != null && icon != null) {
            arrowImage.sprite = icon;
        }
        //enable camera control after dragging
        if (maincam != null) {
            maincam.GetComponent<ArcGISCameraControllerTouch>().enabled = true;
            Debug.Log("Enabled camera control");
        }
        editor.dragUI = false;
    }
}
