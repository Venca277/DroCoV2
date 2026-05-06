// ============================================================
// MissionEditor.cs
// 
// Author: Václav Sovák
// Date: 2026-05-05
// 
// Handles interactive editing of mission waypoints
// in scene. Supports select, multiselect, free drag, 
// axis drag with HandleArrows gizmo, and 
// column drag via ColumnGizmo.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class MissionEditor : MonoBehaviour {

    [Header("Settings")]
    public Camera mainCamera;
    public LayerMask manipLayer;
    public MissionGenerator missionGenerator;

    [Header("Gizmos")]
    public GameObject gizmoPrefab;

    private GameObject gizmo;
    private List<WaypointSelect> selectedWaypoints = new List<WaypointSelect>();
    private ArcGISCameraControllerTouch cam;
    private bool iAmHolding = false;    //free drag active
    private Vector3 lastMouseClick;     //last mouse pos at last frame
    private Plane plane;                //plane for dragging

    //saved tubes and positions for dragging
    private Dictionary<WaypointSelect, List<GameObject>> savedTubes = new Dictionary<WaypointSelect, List<GameObject>>();
    private Dictionary<WaypointSelect, Vector3> lastWPpositions = new Dictionary<WaypointSelect, Vector3>();
    private float lastUpdate = 0f;
    private float updateInterval = 0.05f;
    public bool dragUI = false;

    void Start() {
        cam = mainCamera.GetComponent<ArcGISCameraControllerTouch>();
        UIGizmo.SetSelectedWaypoint(null);
    }

    void Update() {

        //selecting waypoints with click
        if (Input.GetMouseButtonDown(0)) {
            Selected();
        }

        //drag after selected
        if (Input.GetMouseButtonDown(0) && selectedWaypoints.Count > 0) {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 500f, manipLayer);
            bool hitWp = false;
            foreach (var hit in hits) {
                WaypointSelect wp = hit.transform.GetComponent<WaypointSelect>();
                if (wp != null && selectedWaypoints.Contains(wp)) {
                    hitWp = true;
                    break;
                }
            }
            if (hitWp)
                Dragging();
        }

        //deselect all with right click
        if (Input.GetMouseButtonDown(1)) {
            Deselect();
        }

        //update drag on with arrows or free drag
        bool arrdrag = gizmo != null && gizmo.GetComponent<HandleArrows>() != null && gizmo.GetComponent<HandleArrows>().dragging;
        bool columndrag = ColumnGizmo.Instance.dragging;
        if (Input.GetMouseButton(0) && (iAmHolding || arrdrag) && !columndrag) {
            UpdateDragging();
        }

        //stop drag after release
        if (Input.GetMouseButtonUp(0)) {
            iAmHolding = false;
            cam.enabled = true;
        }
    }

    //selects one or more waypoints with click
    private void Selected() {
        selectedWaypoints.RemoveAll(wp => wp == null);
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition); //cast ray
        RaycastHit[] getHit = Physics.RaycastAll(ray, 500f, manipLayer);
        //proceed to cast the ray
        if (getHit.Length > 0) {

            //we check if what we hit is a waypoint
            //take the first one
            WaypointSelect selectedPoint = null;
            foreach (var hit in getHit) {
                if (hit.transform.GetComponent<WaypointSelect>() != null) {
                    selectedPoint = hit.transform.GetComponent<WaypointSelect>();
                    break;
                }
            }

            //if anything was hit to waypoint
            if (selectedPoint != null) {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);

                //more wps can be selected 
                if (!ctrl) {
                    if (gizmo != null)
                        Destroy(gizmo);

                    //deselect selected
                    //clear the list
                    for (int i = 0; i < selectedWaypoints.Count; i++) {
                        selectedWaypoints[i].Select(false);
                        DestroyGizmo(selectedWaypoints[i]);
                    }
                    selectedWaypoints.Clear();
                    selectedWaypoints.Add(selectedPoint);
                    selectedPoint.Select(true);
                    UIGizmo.SetSelectedWaypoint(selectedPoint.transform);

                    //one is selected and no arrows currently
                    if (selectedWaypoints.Count == 1 && gizmo == null) {
                        //create gizmo
                        //assign missioneditor
                        gizmo = new GameObject("arrows");
                        gizmo.transform.position = selectedPoint.transform.position;
                        HandleArrows nav = gizmo.AddComponent<HandleArrows>();
                        nav.wp = selectedPoint.transform;
                        nav.missioneditor = this;
                        ColumnGizmo.Instance.CreateCol(selectedPoint);
                    }
                } else {
                    if (gizmo != null)
                        Destroy(gizmo);
                    selectedWaypoints.Add(selectedPoint);
                    selectedPoint.Select(true);
                    //UIGizmo.SetSelectedWaypoint(selectedPoint.transform);
                }
                Debug.Log("Selected waypoint on: " + selectedPoint.gameObject.transform.position + "total selected: " + selectedWaypoints.Count + "; ");
            }
        }
    }

    //returns world hit on plane
    private Vector3 GetCoords() {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        float pp;
        if (plane.Raycast(ray, out pp)) {
            return ray.GetPoint(pp);
        } else {
            return Vector3.zero;
        }
    }

    private void Dragging() {
        //drag only with arrows
        if (gizmo != null) {
            //send ray and check if we hit the arrows
            Ray r = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(r, out hit)) {
                Transform trans = hit.transform;
                while (trans != null) {
                    if (trans == gizmo.transform) {
                        //update wps and tubes
                        savedTubes.Clear();
                        lastWPpositions.Clear();
                        foreach (var waypoint in selectedWaypoints) {
                            savedTubes[waypoint] = FindTubesWaypoint(waypoint.gameObject);
                            lastWPpositions[waypoint] = waypoint.transform.position;
                        }

                        cam.enabled = false;
                        return;
                    }
                    trans = trans.parent;
                }
            }
        }


        //normal drag for more waypoints and free drag
        if (selectedWaypoints.Count > 0 && !dragUI) {

            //update tubes and save wps
            savedTubes.Clear();
            lastWPpositions.Clear();
            foreach (var waypoint in selectedWaypoints) {
                savedTubes[waypoint] = FindTubesWaypoint(waypoint.gameObject);
                lastWPpositions[waypoint] = waypoint.transform.position;
            }

            //shift key moves waypoint in vertical
            //otherwise free horizontal move
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
            if (shift) {
                plane = new Plane(Vector3.forward, selectedWaypoints[0].transform.position);
                lastMouseClick = GetCoords();
                iAmHolding = true;
            } else {
                plane = new Plane(Vector3.up, selectedWaypoints[0].transform.position);
                lastMouseClick = GetCoords();
                iAmHolding = true;
            }
            cam.enabled = false;
        }
    }

    //returns all tubes connected to a waypoint
    public List<GameObject> FindTubesWaypoint(GameObject wp) {
        if (missionGenerator != null && missionGenerator.waypoints.ContainsKey(wp)) {
            return missionGenerator.waypoints[wp].tubes;
        }
        return new List<GameObject>();
    }

    //recalculate tube positions between two waypoints
    public void UpdateTubes(List<GameObject> tubes, Vector3 oldwaypoint, Vector3 newwaypoint) {
        foreach (GameObject tube in tubes) {

            //recaulculate tube position
            Vector3 oldcenter = tube.transform.position;
            Vector3 startpoint = (oldcenter * 2f) - oldwaypoint;

            //calculate new center
            Vector3 newcenter = (newwaypoint + startpoint) / 2f;
            float distance = Vector3.Distance(newwaypoint, startpoint);
            tube.transform.position = newcenter;
            tube.transform.rotation = Quaternion.identity;
            tube.transform.LookAt(newwaypoint);
            tube.transform.Rotate(90, 0, 0);
            tube.transform.localScale = new Vector3(tube.transform.localScale.x, distance / 2f, tube.transform.localScale.z);
        }
    }

    //update tubes for all selected waypoints
    public void UpdateSelectedTubes() {
        for (int i = 0; i < selectedWaypoints.Count; i++) {
            WaypointSelect wp = selectedWaypoints[i];
            if (!savedTubes.ContainsKey(wp) || !lastWPpositions.ContainsKey(wp))
                continue;

            UpdateTubes(savedTubes[wp], lastWPpositions[wp], wp.transform.position);
            lastWPpositions[wp] = wp.transform.position;
        }
    }

    //updates the position of arrows and the waypoint
    private void UpdateDragging() {
        //drag only with arrows
        bool arrows = gizmo != null && gizmo.GetComponent<HandleArrows>() != null && gizmo.GetComponent<HandleArrows>().dragging;
        if (arrows) {
            //update when time is
            if (Time.time - lastUpdate >= updateInterval) {
                lastUpdate = Time.time;

                //if moved wps have any tubes we update them
                UpdateSelectedTubes();
            }
            return;
        }

        //normal drag for more waypoints and free drag
        if (selectedWaypoints.Count > 0 && iAmHolding && !dragUI) {
            Vector3 curr = GetCoords();
            Vector3 moveoff = curr - lastMouseClick;

            //drag all selected waypoints
            for (int i = 0; i < selectedWaypoints.Count; i++) {
                selectedWaypoints[i].transform.position += moveoff;
            }

            //move arrows if it exists
            if (gizmo != null && selectedWaypoints.Count == 1) {
                gizmo.transform.position = selectedWaypoints[0].transform.position;
            }

            //update when time is
            if (Time.time - lastUpdate >= updateInterval) {
                lastUpdate = Time.time;

                //if moved wps have any tubes we update them
                UpdateSelectedTubes();
            }
            lastMouseClick = curr;
        }
    }

    //deselects all waypoints
    private void Deselect() {
        selectedWaypoints.RemoveAll(wp => wp == null);
        //deselect all
        if (gizmo != null) {
            Destroy(gizmo);
            gizmo = null;
        }

        //destroy tubes and arrows for all selected
        for (int i = 0; i < selectedWaypoints.Count; i++) {
            selectedWaypoints[i].Select(false);
            DestroyGizmo(selectedWaypoints[i]);
        }
        selectedWaypoints.Clear();
        UIGizmo.SetSelectedWaypoint(null);
    }

    //destroy gizmo and deselect waypoint
    private void DestroyGizmo(WaypointSelect wp) {
        GameObject cyl = wp.transform.Find("ColumnDown")?.gameObject;
        GameObject cyl2 = wp.transform.Find("ColumnUp")?.gameObject;
        if (cyl != null)
            Destroy(cyl);
        if (cyl2 != null)
            Destroy(cyl2);
        GameObject gndarrs = GameObject.Find("GroundArrows");
        if (gndarrs != null)
            Destroy(gndarrs);
        UIGizmo.SetSelectedWaypoint(null);
    }

    //adds to selection all waypoints in the selectionBox
    public void SelectBox(List<GameObject> wps) {
        Deselect();
        foreach (GameObject wp in wps) {
            WaypointSelect ws = wp.GetComponent<WaypointSelect>();
            if (ws != null) {
                selectedWaypoints.Add(ws);
                ws.Select(true);
            }
        }
        if (selectedWaypoints.Count > 0)
            UIGizmo.SetSelectedWaypoint(selectedWaypoints[0].transform);
    }

    //init the drag move of the arrows
    public void InitDragArr(WaypointSelect wp) {
        //clear lists
        savedTubes.Clear();
        lastWPpositions.Clear();

        //save tubes and positions for drag
        savedTubes[wp] = FindTubesWaypoint(wp.gameObject);
        lastWPpositions[wp] = wp.transform.position;
        cam.enabled = false;
    }

    //init drag of column gizmo
    public void InitDragColumn(List<WaypointSelect> colWaypoints) {
        //clear list
        savedTubes.Clear();
        lastWPpositions.Clear();

        //save tubes and positions for drag
        foreach (WaypointSelect wp in colWaypoints) {
            savedTubes[wp] = FindTubesWaypoint(wp.gameObject);
            lastWPpositions[wp] = wp.transform.position;
        }
        cam.enabled = false;
    }

    public bool HasMission() {
        return selectedWaypoints.Count > 0;
    }

    //move single waypoint to new position
    public void MoveWaypoint(WaypointSelect wp, Vector3 newPos) {
        Vector3 oldPos = wp.transform.position;
        wp.transform.position = newPos;

        //move wp tubes if any
        if (savedTubes.ContainsKey(wp)) {
            List<GameObject> tubes = savedTubes[wp];
            UpdateTubes(tubes, oldPos, newPos);
            lastWPpositions[wp] = newPos;
        }

        //update gizmo arrows if any
        GameObject wparrows = GameObject.Find("arrows");
        if (wparrows != null) {
            wparrows.transform.position = newPos;
        }
        //update column arrows
        GameObject gndarrs = GameObject.Find("GroundArrows");
        if (gndarrs != null) {
            float y = gndarrs.transform.position.y;
            gndarrs.transform.position = new Vector3(newPos.x, y, newPos.z);
        }
    }

    //moves all selected waypoints by offset
    public void MoveWaypoints(Vector3 off) {
        foreach (WaypointSelect wp in selectedWaypoints) {
            Vector3 oldPos = wp.transform.position;
            wp.transform.position = oldPos + off;
            UpdateTubes(FindTubesWaypoint(wp.gameObject), oldPos, wp.transform.position);
        }

        if (gizmo != null)
            gizmo.transform.position += off;
    }

    //move all waypoints in column
    public void MoveWaypointColumn(List<WaypointSelect> colWaypoints, Vector3 offset) {
        foreach (WaypointSelect wp in colWaypoints) {
            //add offset
            Vector3 oldPos = wp.transform.position;
            Vector3 newPos = oldPos + offset;

            //move waypoint and update tubes
            wp.transform.position = newPos;
            if (!savedTubes.ContainsKey(wp))
                savedTubes[wp] = FindTubesWaypoint(wp.gameObject);

            UpdateTubes(savedTubes[wp], oldPos, newPos);
            lastWPpositions[wp] = newPos;
        }

        //updates gizmo arrows if any
        if (colWaypoints.Count > 0) {
            Vector3 mainWpPos = colWaypoints[0].transform.position;

            //find and update waypoint arrows
            GameObject wparrows = GameObject.Find("arrows");
            if (wparrows != null) {
                wparrows.transform.position = mainWpPos;
            }

            //find and update column arrows
            GameObject gndarrs = GameObject.Find("GroundArrows");
            if (gndarrs != null) {
                float y = gndarrs.transform.position.y;
                gndarrs.transform.position = new Vector3(mainWpPos.x, y, mainWpPos.z);
            }
        }
    }
}
