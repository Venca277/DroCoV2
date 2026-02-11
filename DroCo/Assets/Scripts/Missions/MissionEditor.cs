using System.Collections;
using System.Collections.Generic;
//using System.Numerics;

//using System.Numerics;
//using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class MissionEditor : MonoBehaviour {

    [Header("Settings")]
    public Camera mainCamera;
    public LayerMask manipLayer;

    private List<WaypointSelect> selectedWaypoints = new List<WaypointSelect>();
    private bool iAmHolding = false;
    private Vector3 lastMouseClick;
    private Plane plane;

    private Dictionary<WaypointSelect, List<GameObject>> savedTubes = new Dictionary<WaypointSelect, List<GameObject>>();
    private Dictionary<WaypointSelect, Vector3> lastWPpositions = new Dictionary<WaypointSelect, Vector3>();
    private float lastUpdate = 0f;
    private float updateInterval = 0.05f;

    void Start() {

    }

    // Update is called once per frame
    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            Selected();
        }

        if (Input.GetMouseButtonDown(0) && selectedWaypoints.Count > 0) {
            Dragging();
        }

        if (Input.GetMouseButtonDown(1)) {
            Deselect();
        }

        if (Input.GetMouseButton(0) && iAmHolding) {
            UpdateDragging();
        }

        if (Input.GetMouseButtonUp(0)) {
            iAmHolding = false;
            mainCamera.GetComponent<ArcGISCameraControllerTouch>().enabled = true;
        }
    }

    private void Selected() {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition); // cast ray to identify the hit
        RaycastHit[] getHit = Physics.RaycastAll(ray, 500f, manipLayer);                                          // information of hit object
        //proceed to cast the ray
        if (getHit.Length > 0) {

            WaypointSelect selectedPoint = null;
            foreach (var hit in getHit) {
                if (hit.transform.GetComponent<WaypointSelect>() != null) {
                    selectedPoint = hit.transform.GetComponent<WaypointSelect>();
                    break;
                }
            }

            if (selectedPoint != null) {
                bool holdingCTRL = Input.GetKey(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);

                if (!holdingCTRL) {
                    for (int i = 0; i < selectedWaypoints.Count; i++) {
                        selectedWaypoints[i].Select(false);
                    }
                    selectedWaypoints.Clear();
                    selectedWaypoints.Add(selectedPoint);
                    selectedPoint.Select(true);
                } else {
                    selectedWaypoints.Add(selectedPoint);
                    selectedPoint.Select(true);
                }
                Debug.Log("Selected waypoint on: " + selectedPoint.gameObject.transform.position + "; total selected: " + selectedWaypoints.Count + "; ");
            }
        }
    }

    private Vector3 GetCoords() {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        float pp;
        if (plane.Raycast(ray, out pp)) {
            return ray.GetPoint(pp);
        } else {
            return Vector3.zero;
        }
        //iAmHolding = true;
    }

    private void Dragging() {
        if (selectedWaypoints.Count > 0) {
            savedTubes.Clear();
            lastWPpositions.Clear();
            foreach (var waypoint in selectedWaypoints) {
                savedTubes[waypoint] = FindTube(waypoint.gameObject);
                lastWPpositions[waypoint] = waypoint.transform.position;
            }

            bool holdingSHIFT = Input.GetKey(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
            if (holdingSHIFT) {
                plane = new Plane(Vector3.forward, selectedWaypoints[0].transform.position); // vertical normal in place of first waypoint
                lastMouseClick = GetCoords();
                iAmHolding = true;
            } else {
                plane = new Plane(Vector3.up, selectedWaypoints[0].transform.position); // horizontal normal in place of first waypoint
                lastMouseClick = GetCoords();
                iAmHolding = true;
            }
            mainCamera.GetComponent<ArcGISCameraControllerTouch>().enabled = false;
        }
    }

    public List<GameObject> FindTube(GameObject waypoint) {
        Ray ray = new Ray(waypoint.transform.position, Vector3.zero);
        Collider[] hits = Physics.OverlapSphere(waypoint.transform.position, 0.8f, LayerMask.GetMask("Mission"));
        Debug.Log("Found " + hits.Length + " colliders near waypoint.");
        List<GameObject> tubes = new List<GameObject>();
        if (hits.Length > 0) {
            foreach (var hit in hits) {
                Debug.Log("Hit object: " + hit.gameObject.name);
                GameObject tube = hit.gameObject;
                if (tube.name.StartsWith("Tube_Segment")) {
                    tubes.Add(tube);
                }
                if (tubes.Count >= 2) {
                    return tubes;
                }
            }
        }
        return tubes;
    }

    public void UpdateTubes(List<GameObject> tubes, Vector3 oldwaypoint, WaypointSelect newwaypoint) {
        foreach (var tube in tubes) {
            Vector3 oldcenter = tube.transform.position;
            Vector3 waypointoldpos = oldwaypoint;
            Vector3 startpoint = (oldcenter * 2f) - waypointoldpos;

            Vector3 newcenter = (newwaypoint.transform.position + startpoint) / 2f;
            float distance = Vector3.Distance(newwaypoint.transform.position, startpoint);
            tube.transform.position = newcenter;
            tube.transform.rotation = Quaternion.identity;
            tube.transform.LookAt(newwaypoint.transform.position);
            tube.transform.Rotate(90, 0, 0);
            tube.transform.localScale = new Vector3(tube.transform.localScale.x, distance / 2f, tube.transform.localScale.z);
        }
    }

    private void UpdateDragging() {
        if (selectedWaypoints.Count > 0) {
            Vector3 curr = GetCoords();
            Vector3 moveoff = curr - lastMouseClick;

            for (int i = 0; i < selectedWaypoints.Count; i++) {
                selectedWaypoints[i].transform.position += moveoff;
            }

            if (Time.time - lastUpdate >= updateInterval) {
                lastUpdate = Time.time;

                for (int i = 0; i < selectedWaypoints.Count; i++) {
                    if (savedTubes.ContainsKey(selectedWaypoints[i])) {
                        lastWPpositions.ContainsKey(selectedWaypoints[i]);

                        List<GameObject> tubes = savedTubes[selectedWaypoints[i]];
                        Vector3 oldwaypoint = lastWPpositions[selectedWaypoints[i]];

                        UpdateTubes(tubes, oldwaypoint, selectedWaypoints[i]);

                        lastWPpositions[selectedWaypoints[i]] = selectedWaypoints[i].transform.position;
                    }
                }
            }
            lastMouseClick = curr;
        }
    }

    private void Deselect() {
        for (int i = 0; i < selectedWaypoints.Count; i++) {
            selectedWaypoints[i].Select(false);
        }
        selectedWaypoints.Clear();
    }

    public bool HasMission() {
        return selectedWaypoints.Count > 0;
    }
}
