using System.Collections.Generic;
using UnityEngine;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Geometry;
using UnityEngine.UI;
using Unity.VisualScripting;
using TMPro;
using UnityEditor.VersionControl;

[System.Serializable]
public class GPSWaypoint {
    public double latitude;
    public double longitude;
    public double altitude;
}

public class MissionGenerator : MonoBehaviour {

    [Header("ArcGIS Reference")]
    public ArcGISMapComponent mapComponent; //map object from hierarchy

    [Header("Flight Path Parameters")]
    [Tooltip("Distance of orbit from building")]
    public float scanDistance = 2.0f;

    [Tooltip("Vertical step")]
    public float verticalStep = 1.5f;

    [Tooltip("Length of segments")]
    public float maxSegmentLen = 6.0f;

    [Header("Path visual")]
    public bool use3DTubes = true;
    public Color pathColor = Color.blue;
    public float tubeThickness = 0.2f;

    [Header("Waypoints")]
    public GameObject waypointPrefab;
    public GameObject waypointUIPrefab;
    public float waypointSize = 0.3f;

    [Header("Icons")]
    public Sprite mapActiveIcon;
    public Sprite mapIcon;

    [Header("Collision")]
    public float droneRadius = 0.5f;
    public LayerMask collisionLayer;
    public float maxPush = 15.0f;

    private LineRenderer lineRenderer;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    public Dictionary<GameObject, List<GameObject>> tubeMap = new Dictionary<GameObject, List<GameObject>>();

    void Awake() {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        int layerIndex = LayerMask.NameToLayer("Buildings");
        gameObject.layer = (layerIndex != -1) ? layerIndex : 0;

        lineRenderer.startWidth = tubeThickness;
        lineRenderer.endWidth = tubeThickness;
        lineRenderer.useWorldSpace = true;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = pathColor;
        lineRenderer.material = mat;
    }

    public List<Vector3> GenerateScanPath(GameObject buildingObj, List<Vector3> footprintPoints) {
        ClearPath();

        if (buildingObj == null || footprintPoints == null || footprintPoints.Count < 3)
            return new List<Vector3>();

        Bounds bounds = buildingObj.GetComponent<Collider>().bounds;
        float startY = bounds.min.y + 2.0f;
        float endY = bounds.max.y + 1.0f;

        //orbital ring generation
        List<Vector3> orbitRing = new List<Vector3>();
        Vector3 centroid = Vector3.zero;
        foreach (var p in footprintPoints)
            centroid += p;
        centroid /= footprintPoints.Count;
        centroid.y = 0;

        List<Vector3> sepFootprint = new List<Vector3>();
        for (int i = 0; i < footprintPoints.Count; i++) {
            Vector3 p1 = footprintPoints[i];
            int k = i + 1;
            if (k >= footprintPoints.Count)
                k = 0;
            Vector3 p2 = footprintPoints[k];
            p1.y = 0;
            p2.y = 0;

            sepFootprint.Add(p1);

            float dist = Vector3.Distance(p1, p2);
            float maxSeg = maxSegmentLen;

            if (dist > maxSeg) {
                int steps = Mathf.CeilToInt(dist / maxSeg);
                for (int j = 1; j < steps; j++) {
                    sepFootprint.Add(Vector3.Lerp(p1, p2, (float) j / steps));
                }
            }
        }

        for (int i = 0; i < sepFootprint.Count; i++) {
            Vector3 curr = sepFootprint[i];
            Vector3 prev = sepFootprint[(i - 1 + sepFootprint.Count) % sepFootprint.Count];
            Vector3 next = sepFootprint[(i + 1) % sepFootprint.Count];

            //vectors of the walls
            Vector3 dirPrev = (curr - prev).normalized;
            Vector3 dirNext = (next - curr).normalized;

            //perpendicular vects
            Vector3 normPrev = new Vector3(-dirPrev.z, 0, dirPrev.x);
            if (Vector3.Dot(normPrev, curr - centroid) < 0)
                normPrev = -normPrev; //point out always

            Vector3 normNext = new Vector3(-dirNext.z, 0, dirNext.x);
            if (Vector3.Dot(normNext, curr - centroid) < 0)
                normNext = -normNext;

            //average normal for the vertex
            Vector3 vertexNormal = (normPrev + normNext).normalized;
            if (vertexNormal == Vector3.zero)
                vertexNormal = normPrev;

            //move the offset of scandist
            Vector3 offsetPoint = curr + (vertexNormal * scanDistance);

            if (orbitRing.Count == 0 || Vector3.Distance(offsetPoint, orbitRing[orbitRing.Count - 1]) > 0.1f) {
                orbitRing.Add(offsetPoint);
            }
        }

        /*
        foreach (var p in footprintPoints) {
            Vector3 pointFlat = new Vector3(p.x, 0, p.z);
            Vector3 dir = (pointFlat - centroid).normalized;
            Vector3 offsetPoint = pointFlat + (dir * scanDistance);
            if (orbitRing.Count == 0 || Vector3.Distance(offsetPoint, orbitRing[orbitRing.Count - 1]) > 0.1f)
                orbitRing.Add(offsetPoint);
        }

        if (orbitRing.Count > 1 && Vector3.Distance(orbitRing[0], orbitRing[orbitRing.Count - 1]) < 0.1f) {
            orbitRing.RemoveAt(orbitRing.Count - 1);
        }
        */

        //helix generation
        List<Vector3> finalPath = new List<Vector3>();
        float currentY = startY;
        Vector3 lastPoint = Vector3.zero;
        bool isFirst = true;

        while (currentY < endY) {
            for (int i = 0; i < orbitRing.Count; i++) {
                float progress = (float) i / orbitRing.Count;
                float heightOffset = verticalStep * progress;
                float actualY = currentY + heightOffset;

                if (actualY > endY)
                    break;

                Vector3 pt = orbitRing[i];
                Vector3 propPos = new Vector3(pt.x, actualY, pt.z);

                Vector3 currCenter = new Vector3(centroid.x, actualY, centroid.z);
                Vector3 pushDir = (propPos - currCenter).normalized;

                Vector3 noCollisionPos = SolveCollision(propPos, pushDir);

                if (!isFirst && Vector3.Distance(lastPoint, noCollisionPos) < 0.2f) {
                    continue;
                }

                if (!isFirst) {
                    List<Vector3> sightPoints = SolveSightline(lastPoint, noCollisionPos, pushDir);
                    finalPath.AddRange(sightPoints);
                    lastPoint = sightPoints[sightPoints.Count - 1];
                } else {
                    finalPath.Add(noCollisionPos);
                    lastPoint = noCollisionPos;
                    isFirst = false;
                }

                //finalPath.Add(noCollisionPos);
                //lastPoint = noCollisionPos;
                //isFirst = false;
            }
            currentY += verticalStep;
        }

        //renderer
        VisualizePath(finalPath);

        //AddMissionToUI();
        //return unity coords
        return finalPath;
    }

    private Vector3 SolveCollision(Vector3 targetpos, Vector3 pushDir) {
        Vector3 curr = targetpos;
        float pushed = 0.0f;
        float step = 0.5f;
        float maxHorizPush = 3.0f;

        while (isPosBlocked(curr)) {
            curr += pushDir * step;
            pushed += step;

            if (pushed > maxHorizPush) {
                Debug.LogWarning("Max push exceeded, returning original position");
                return findSafeAlt(targetpos);
            }
        }
        return curr;
    }

    private Vector3 findSafeAlt(Vector3 pos) {
        Vector3 sky = new Vector3(pos.x, pos.y + 100.0f, pos.z);
        RaycastHit hit;

        if (Physics.Raycast(sky, Vector3.down, out hit, 200f, collisionLayer)) {
            float safeY = hit.point.y + droneRadius + 1.0f;

            safeY = Mathf.Max(safeY, pos.y);

            return new Vector3(pos.x, safeY, pos.z);
        }

        return pos;
    }

    private float getHighestAlt(Vector3 prevPoint, Vector3 currPoint) {
        float highest = Mathf.Max(prevPoint.y, currPoint.y);
        float dist = Vector3.Distance(prevPoint, currPoint);

        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.5f));

        for (int i = 1; i < steps; i++) {
            float t = (float) i / steps;
            Vector3 pos = Vector3.Lerp(prevPoint, currPoint, t);

            float alt = findSafeAlt(pos).y;
            if (alt > highest) {
                highest = alt;
            }
        }
        return highest;
    }

    private bool isPosBlocked(Vector3 pos) {
        if (Physics.CheckSphere(pos, droneRadius, collisionLayer)) {
            return true;
        }

        Vector3 sky = new Vector3(pos.x, pos.y + 100.0f, pos.z);
        if (Physics.Linecast(sky, pos, collisionLayer)) {
            return true;
        }

        return false;
    }

    private List<Vector3> SolveSightline(Vector3 prevPoint, Vector3 currPoint, Vector3 pushDir) {
        List<Vector3> segPoints = new List<Vector3>();
        Vector3 finPoint = currPoint;
        RaycastHit hit;
        //float pushed = 0.0f;
        //float step = 0.5f;

        Vector3 dir = finPoint - prevPoint;
        float dist = dir.magnitude;

        if (dist > 0.1f && Physics.SphereCast(prevPoint, droneRadius, dir.normalized, out hit, dist, collisionLayer)) {
            float safeAlt = getHighestAlt(prevPoint, currPoint);
            Vector3 risePoint = new Vector3(prevPoint.x, safeAlt, prevPoint.z);

            if (Vector3.Distance(prevPoint, risePoint) > 0.1f) {
                segPoints.Add(risePoint);
            }

            Vector3 dropPoint = new Vector3(currPoint.x, safeAlt, currPoint.z);
            if (Vector3.Distance(risePoint, dropPoint) > 0.1f) {
                segPoints.Add(dropPoint);
            }

            if (Vector3.Distance(dropPoint, currPoint) > 0.1f) {
                segPoints.Add(currPoint);
            }
        } else {
            segPoints.Add(currPoint);
        }

        return segPoints;
    }

    public List<GPSWaypoint> ConvertToGPSCoordinates(List<Vector3> unityPath) {
        List<GPSWaypoint> gpsPath = new List<GPSWaypoint>();

        if (mapComponent == null) {
            Debug.LogError("MissionGenerator: Není přiřazena ArcGIS Map Component! Nelze převádět souřadnice.");
            return gpsPath;
        }

        foreach (var point in unityPath) {
            //relative unity coords to gps coords
            ArcGISPoint geoPos = mapComponent.EngineToGeographic(point);

            GPSWaypoint wp = new GPSWaypoint();
            wp.latitude = geoPos.Y;  // Y is lat
            wp.longitude = geoPos.X; // X is lon
            wp.altitude = geoPos.Z;  // Z is alt

            gpsPath.Add(wp);
        }

        return gpsPath;
    }

    public void VisualizePath(List<Vector3> path) {
        if (path.Count == 0)
            return;

        if (use3DTubes)
            lineRenderer.enabled = false;
        else {
            lineRenderer.positionCount = path.Count;
            lineRenderer.SetPositions(path.ToArray());
        }

        GameObject prevP = null;

        for (int i = 0; i < path.Count; i++) {
            Vector3 currentPos = path[i];

            GameObject currP = CreateWaypointMarker(currentPos, i);
            tubeMap[currP] = new List<GameObject>();

            if (use3DTubes && i > 0) {
                GameObject tube = CreateTubeSegment(path[i - 1], currentPos, i - 1);

                tubeMap[currP].Add(tube);
                tubeMap[prevP].Add(tube);
            }
            prevP = currP;
        }
    }

    private GameObject CreateWaypointMarker(Vector3 pos, int index) {
        GameObject wpObj;
        if (waypointPrefab != null) {
            wpObj = Instantiate(waypointPrefab, pos, Quaternion.identity);
        } else {
            wpObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var renderer = wpObj.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader)
                shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = Color.yellow;
            renderer.material = mat;
        }
        wpObj.name = $"WP_{index}";
        wpObj.transform.position = pos;
        wpObj.transform.localScale = Vector3.one * waypointSize;
        int layerIndex = LayerMask.NameToLayer("Mission");
        wpObj.layer = (layerIndex != -1) ? layerIndex : 0;
        spawnedObjects.Add(wpObj);
        return wpObj;
    }

    private GameObject CreateTubeSegment(Vector3 start, Vector3 end, int index) {
        GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tube.name = "Tube_Segment_" + index;
        Collider col = tube.GetComponent<Collider>();
        if (col != null) {
            //TODO will set to off later
            col.enabled = false;
            col.isTrigger = false;
        } else {
            Debug.LogWarning("Doesn't have collider!");
        }

        int layerIndex = LayerMask.NameToLayer("Mission");
        tube.layer = (layerIndex != -1) ? layerIndex : 0;
        var renderer = tube.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader)
            shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = pathColor;
        mat.SetFloat("_Smoothness", 0.5f);
        renderer.material = mat;

        Vector3 centerPos = (start + end) / 2f;
        float distance = Vector3.Distance(start, end);
        tube.transform.position = centerPos;
        tube.transform.LookAt(end);
        tube.transform.Rotate(90, 0, 0);
        tube.transform.localScale = new Vector3(tubeThickness, distance / 2f, tubeThickness);
        spawnedObjects.Add(tube);
        return tube;
    }

    public void ClearPath() {
        lineRenderer.positionCount = 0;
        foreach (var obj in spawnedObjects) {
            if (obj != null)
                Destroy(obj);
        }
        spawnedObjects.Clear();
        tubeMap.Clear();
    }

    public bool HasMission() {
        return spawnedObjects.Count > 0;
    }

    public void AddMissionToUI() {
        GameObject missionList = GameObject.Find("MissionsListContainer");
        if (missionList == null) {
            Debug.LogWarning("couldnt find missionlist");
            return;
        }

        Transform content = missionList.transform.Find("Content");
        if (content == null) {
            Debug.LogWarning("couldnt find content");
            return;
        }
        Transform header = missionList.transform.Find("Header");
        if (header == null) {
            Debug.LogWarning("couldnt find header");
            return;
        }

        TMP_InputField scanDistance = content.Find("Row1/scanDistance").GetComponent<TMP_InputField>();
        TMP_InputField verticalStep = content.Find("Row2/verticalStep").GetComponent<TMP_InputField>();
        Toggle use3DTubes = content.Find("Row3/use3Dtubes").GetComponent<Toggle>();
        TMP_Dropdown pathColor = content.Find("Row4/pathColor").GetComponent<TMP_Dropdown>();
        TMP_InputField tubeThickness = content.Find("Row5/tubeThikness").GetComponent<TMP_InputField>();
        TMP_InputField waypointSize = content.Find("Row6/waypointSize").GetComponent<TMP_InputField>();
        if (scanDistance == null || verticalStep == null || use3DTubes == null || pathColor == null || tubeThickness == null || waypointSize == null) {
            Debug.LogWarning($"couldnt find UI elements: {scanDistance}, {verticalStep}, {use3DTubes}, {pathColor}, {tubeThickness}, {waypointSize}");
            return;
        }

        scanDistance.text = $"{this.scanDistance}";
        verticalStep.text = $"{this.verticalStep}";
        use3DTubes.isOn = this.use3DTubes;
        pathColor.options.Clear();
        pathColor.options.Add(new TMP_Dropdown.OptionData("Blue"));
        pathColor.options.Add(new TMP_Dropdown.OptionData("Red"));
        pathColor.options.Add(new TMP_Dropdown.OptionData("Green"));
        pathColor.options.Add(new TMP_Dropdown.OptionData("Yellow"));
        pathColor.options.Add(new TMP_Dropdown.OptionData("Cyan"));
        pathColor.options.Add(new TMP_Dropdown.OptionData("Magenta"));
        pathColor.value = 0;
        tubeThickness.text = $"{this.tubeThickness}";
        waypointSize.text = $"{this.waypointSize}";

        Image headerImage = header.Find("Image").GetComponent<Image>();
        if (headerImage != null) {
            if (mapActiveIcon != null) {
                headerImage.sprite = mapActiveIcon;
            } else {
                Debug.LogWarning("couldnt find MapActive sprite");
            }
        } else {
            Debug.LogWarning("couldnt find header image");
        }
    }

    private void selectedUIWaypoint(GameObject clickedWP) {
        //TODO select the waypoint
    }
}
