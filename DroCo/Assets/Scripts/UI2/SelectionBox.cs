using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionBox : MonoBehaviour {
    [Header("Reference")]
    public MissionGenerator missionGenerator;
    public MissionController missionController;
    public RectTransform box;
    public Camera mainCamera;

    [Header("Selection")]
    public Color selectedColor = Color.red;
    private Color defaultColor = Color.yellow;

    private Vector2 mousePos;
    private List<GameObject> selectedWps = new List<GameObject>();
    private bool dragging = false;

    void Start() {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (box != null)
            box.gameObject.SetActive(false);
    }

    void Update() {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

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
        }

        if (Input.GetMouseButtonDown(1)) {
            ClearSelection();
        }

        //deleting
        if (Input.GetKeyDown(KeyCode.Delete)) {
            DeleteWaypoints();
        }
    }

    private void BoxStrech(Vector2 pos) {
        float width = pos.x - mousePos.x;
        float height = pos.y - mousePos.y;

        box.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        box.anchoredPosition = new Vector2(
            Mathf.Min(mousePos.x, pos.x),
            Mathf.Min(mousePos.y, pos.y)
        );
    }

    private void SelectWaypoints() {
        if (missionGenerator == null)
            return;

        Rect selectbox = new Rect(
            box.anchoredPosition.x,
            box.anchoredPosition.y,
            box.sizeDelta.x,
            box.sizeDelta.y
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

    private void DeleteWaypoints() {
        if (selectedWps.Count == 0 || missionGenerator == null)
            return;

        List<GameObject> pointsToProcess = new List<GameObject>(selectedWps);

        if (missionController != null) {
            missionController.DeleteWaypoints(pointsToProcess);
        }

        selectedWps.Clear();
    }

    public void ClearSelection() {
        foreach (GameObject wp in selectedWps) {
            if (wp != null)
                SelectWp(wp, false);
        }
        selectedWps.Clear();
    }

    private void SelectWp(GameObject wp, bool sel) {
        MeshRenderer rend = wp.GetComponent<MeshRenderer>();
        if (rend != null)
            rend.material.color = sel ? selectedColor : defaultColor;
    }
}