// ============================================================
// SelectionBox.cs
//
// Author: Václav Sovák
// Date: 2026-05-05
//
// Shift+drag selection box for waypoints in the 3D space.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionBox : MonoBehaviour {
    [Header("Reference")]
    public MissionGenerator missionGenerator;
    public MissionController missionController;
    public MissionEditor missionEditor;
    public RectTransform box;
    public Camera mainCamera;

    [Header("Selection")]
    public Color selectedColor = Color.red;
    private Color defaultColor = Color.yellow;

    private Vector2 mousePos;
    private List<GameObject> selectedWps = new List<GameObject>();
    private bool dragging = false;
    private Canvas canvas;

    void Start() {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (box != null)
            box.gameObject.SetActive(false);
        canvas = box.GetComponentInParent<Canvas>();
    }

    void Update() {
        //ignore drag over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        //start dragging
        if (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) {
            dragging = true;
            mousePos = Input.mousePosition;
            box.gameObject.SetActive(true);
            mainCamera.GetComponent<ArcGISCameraControllerTouch>().enabled = false;
            ClearSelection();
        }

        //strech the box
        if (Input.GetMouseButton(0) && dragging) {
            BoxStrech(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0) && dragging) {
            dragging = false;
            box.gameObject.SetActive(false);
            mainCamera.GetComponent<ArcGISCameraControllerTouch>().enabled = true;
            SelectWaypoints();
            if (missionEditor != null && selectedWps.Count > 0) {
                missionEditor.SelectBox(selectedWps);
            }
        }

        if (Input.GetMouseButtonDown(1)) {
            ClearSelection();
        }

        if (Input.GetKeyDown(KeyCode.Delete)) {
            DeleteWaypoints();
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.Z)) {
            missionController.Undo();
        }
    }

    //resize the selection box on drag
    private void BoxStrech(Vector2 pos) {
        float scale = canvas.scaleFactor;
        box.anchoredPosition = new Vector2(
            Mathf.Min(mousePos.x, pos.x) / scale,
            Mathf.Min(mousePos.y, pos.y) / scale
        );
        box.sizeDelta = new Vector2(
            Mathf.Abs(pos.x - mousePos.x) / scale,
            Mathf.Abs(pos.y - mousePos.y) / scale
        );
    }

    //select waypoints inside the box
    private void SelectWaypoints() {
        if (missionGenerator == null)
            return;

        float scale = canvas.scaleFactor;
        Rect selectbox = new Rect(
            box.anchoredPosition.x * scale,
            box.anchoredPosition.y * scale,
            box.sizeDelta.x * scale,
            box.sizeDelta.y * scale
        );

        List<GameObject> allWps = missionGenerator.GetMissionWaypoints();

        foreach (GameObject wp in allWps) {
            if (wp == null)
                continue;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(wp.transform.position);

            if (screenPos.z > 0 && selectbox.Contains(new Vector2(screenPos.x, screenPos.y))) {
                selectedWps.Add(wp);
                SelectWp(wp, true);
            }
        }
    }

    //delete selected waypoints
    private void DeleteWaypoints() {
        if (selectedWps.Count == 0 || missionGenerator == null)
            return;

        List<GameObject> pointsToProcess = new List<GameObject>(selectedWps);

        if (missionController != null) {
            missionController.DeleteWaypoints(pointsToProcess);
        }

        selectedWps.Clear();
    }

    //clear selection and reset colors
    public void ClearSelection() {
        foreach (GameObject wp in selectedWps) {
            if (wp != null)
                SelectWp(wp, false);
        }
        selectedWps.Clear();
    }

    //highlight or unhighlight a waypoint
    private void SelectWp(GameObject wp, bool sel) {
        MeshRenderer rend = wp.GetComponent<MeshRenderer>();
        if (rend != null)
            rend.material.color = sel ? selectedColor : defaultColor;
    }
}