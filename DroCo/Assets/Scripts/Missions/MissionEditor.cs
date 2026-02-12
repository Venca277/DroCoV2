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

    [Header("Gizmos")]
    public GameObject gizmoPrefab;

    private GameObject gizmo;
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

    void Update() {

        //selecting waypoints with click
        if (Input.GetMouseButtonDown(0)) {
            Selected();
        }

        //drag after selected
        if (Input.GetMouseButtonDown(0) && selectedWaypoints.Count > 0) {
            Dragging();
        }

        //deselect all with right click
        if (Input.GetMouseButtonDown(1)) {
            Deselect();
        }

        //update drag on with arrows or free drag
        bool arrdrag = gizmo != null && gizmo.GetComponent<HandleArrows>() != null && gizmo.GetComponent<HandleArrows>().dragging;
        if (Input.GetMouseButton(0) && (iAmHolding || arrdrag)) {
            UpdateDragging();
        }

        //stop drag after release
        if (Input.GetMouseButtonUp(0)) {
            iAmHolding = false;
            mainCamera.GetComponent<ArcGISCameraControllerTouch>().enabled = true;
        }
    }

    private void Selected() {
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
                bool CTRL = Input.GetKey(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);

                //more wps can be selected 
                if (!CTRL) {
                    if (gizmo != null)
                        Destroy(gizmo);

                    //deselect selected
                    //clear the list
                    for (int i = 0; i < selectedWaypoints.Count; i++) {
                        selectedWaypoints[i].Select(false);
                    }
                    selectedWaypoints.Clear();
                    selectedWaypoints.Add(selectedPoint);
                    selectedPoint.Select(true);

                    //one is selected and no arrows currently
                    //TODO: support gizmoing with more selected
                    if (selectedWaypoints.Count == 1 && gizmo == null) {
                        //create gizmo
                        //assign missioneditor
                        gizmo = new GameObject("arrows");
                        gizmo.transform.position = selectedPoint.transform.position;
                        HandleArrows nav = gizmo.AddComponent<HandleArrows>();
                        nav.wp = selectedPoint.transform;
                        nav.missioneditor = this;
                    }
                } else {
                    if (gizmo != null)
                        Destroy(gizmo);
                    selectedWaypoints.Add(selectedPoint);
                    selectedPoint.Select(true);
                }
                Debug.Log("Selected waypoint on: " + selectedPoint.gameObject.transform.position + "total selected: " + selectedWaypoints.Count + "; ");
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
                            savedTubes[waypoint] = FindTube(waypoint.gameObject);
                            lastWPpositions[waypoint] = waypoint.transform.position;
                        }

                        mainCamera.GetComponent<ArcGISCameraControllerTouch>().enabled = false;
                        return;
                    }
                    trans = trans.parent;
                }
            }
        }

        //normal drag for more waypoints and free drag
        if (selectedWaypoints.Count > 0) {

            //update tubes and save wps
            savedTubes.Clear();
            lastWPpositions.Clear();
            foreach (var waypoint in selectedWaypoints) {
                savedTubes[waypoint] = FindTube(waypoint.gameObject);
                lastWPpositions[waypoint] = waypoint.transform.position;
            }

            //shift moves waypoint in vertical
            //otherwise free horizontal move
            bool SHIFT = Input.GetKey(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
            if (SHIFT) {
                plane = new Plane(Vector3.forward, selectedWaypoints[0].transform.position);
                lastMouseClick = GetCoords();
                iAmHolding = true;
            } else {
                plane = new Plane(Vector3.up, selectedWaypoints[0].transform.position);
                lastMouseClick = GetCoords();
                iAmHolding = true;
            }
            mainCamera.GetComponent<ArcGISCameraControllerTouch>().enabled = false;
        }
    }

    public List<GameObject> FindTube(GameObject waypoint) {
        //casting with sphere search to detect any pipes
        //any Tube_Segment objs are added to the list
        Ray ray = new Ray(waypoint.transform.position, Vector3.zero);
        Collider[] hits = Physics.OverlapSphere(waypoint.transform.position, 0.8f, LayerMask.GetMask("Mission"));
        Debug.Log("Found " + hits.Length + " colliders near waypoint.");
        List<GameObject> tubes = new List<GameObject>();

        if (hits.Length > 0) {
            foreach (var hit in hits) {
                //Debug.Log("Hit object: " + hit.gameObject.name);

                //check for tubes and save them
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
            //recaulculate tube position
            //we have to keep the start end
            //center changes and end end changes
            Vector3 oldcenter = tube.transform.position;
            Vector3 waypointoldpos = oldwaypoint;
            Vector3 startpoint = (oldcenter * 2f) - waypointoldpos;

            //calculate new center
            //keep the distance
            //new position from drag
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
        //drag only with arrows
        bool arrows = gizmo != null && gizmo.GetComponent<HandleArrows>() != null && gizmo.GetComponent<HandleArrows>().dragging;
        if (arrows) {
            //update when time is
            if (Time.time - lastUpdate >= updateInterval) {
                lastUpdate = Time.time;

                //if moved wps have any tubes we update them
                for (int i = 0; i < selectedWaypoints.Count; i++) {
                    if (savedTubes.ContainsKey(selectedWaypoints[i]) && lastWPpositions.ContainsKey(selectedWaypoints[i])) {
                        List<GameObject> tubes = savedTubes[selectedWaypoints[i]];
                        Vector3 oldwaypoint = lastWPpositions[selectedWaypoints[i]];

                        UpdateTubes(tubes, oldwaypoint, selectedWaypoints[i]);

                        lastWPpositions[selectedWaypoints[i]] = selectedWaypoints[i].transform.position;
                    }
                }
            }
            return;
        }

        //normal drag for more waypoints and free drag
        if (selectedWaypoints.Count > 0) {
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
        //deselect all
        if (gizmo != null) {
            Destroy(gizmo);
            gizmo = null;
        }

        for (int i = 0; i < selectedWaypoints.Count; i++) {
            selectedWaypoints[i].Select(false);
        }
        selectedWaypoints.Clear();
    }

    public void InitDragArr(WaypointSelect wp) {
        //clear lists
        savedTubes.Clear();
        lastWPpositions.Clear();

        //save tubes and positions for drag
        savedTubes[wp] = FindTube(wp.gameObject);
        lastWPpositions[wp] = wp.transform.position;
        mainCamera.GetComponent<ArcGISCameraControllerTouch>().enabled = false;
    }

    public bool HasMission() {
        return selectedWaypoints.Count > 0;
    }
}
