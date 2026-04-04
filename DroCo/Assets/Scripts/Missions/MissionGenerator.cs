using System.Collections.Generic;
using UnityEngine;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Geometry;
using System.Linq;
//using UnityEditor.VersionControl;

[System.Serializable]
public class GPSWaypoint {
    public double latitude;
    public double longitude;
    public double altitude;

    public float speed = 5.0f;
    public float heading = 0.0f;
    public float gimbal_pitch = -45.0f;
    public float gimbal_yaw = 0.0f;
}

public enum MissionType {
    Vertical,
    Horizontal
}

public enum MissionStyle {
    Collision,
    Snake
}

public class MissionGenerator : MonoBehaviour {

    [Header("ArcGIS Reference")]
    public ArcGISMapComponent mapComponent; //map object

    [Header("Flight Path Parameters")]
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
    public Texture2D startTexture;
    public Texture2D finishTexture;
    public GameObject waypointPrefab;
    public Material tubeMaterial;
    //public GameObject waypointUIPrefab;
    public float waypointSize = 0.3f;
    public float flightSpeed = 5.0f;

    [Header("Collision")]
    public float droneRadius = 0.5f;
    public LayerMask collisionLayer;

    [Header("Cam")]
    public Camera arcGISCamera;
    public float senzWidth = 6.17f;
    public float senzHeight = 4.55f;
    public float focalLength = 4.67f;
    public float widthOverlap = 0.8f;
    public float heightOverlap = 0.8f;
    public float photoInterval = 2.0f;
    public MissionType missionType = MissionType.Vertical;
    public MissionStyle missionStyle = MissionStyle.Collision;

    private int layerMission;
    private int layerBuildings;
    private LineRenderer lineRenderer;
    public List<GameObject> spawnedObjects = new List<GameObject>();
    public Dictionary<GameObject, List<GameObject>> tubeMap = new Dictionary<GameObject, List<GameObject>>();
    public Dictionary<GameObject, int> waypointLevel = new Dictionary<GameObject, int>();
    public List<Vector3> helixNormals = new List<Vector3>();
    public Vector3 lastcentroid = Vector3.zero;

    void Awake() {
        layerMission = LayerMask.NameToLayer("Mission");
        layerBuildings = LayerMask.NameToLayer("Buildings");

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        gameObject.layer = layerBuildings;

        lineRenderer.startWidth = tubeThickness;
        lineRenderer.endWidth = tubeThickness;
        lineRenderer.useWorldSpace = true;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = pathColor;
        lineRenderer.material = mat;

        tubeMaterial = new Material(shader);
        tubeMaterial.color = pathColor;
        tubeMaterial.SetFloat("_Smoothness", 0.5f);
    }

    public List<Vector3> GenerateScanPath(GameObject buildingObj, List<Vector3> footprintPoints) {
        //reset previous mission
        RecalculateSteps();
        ClearPath();
        helixNormals.Clear();

        if (buildingObj == null || footprintPoints == null || footprintPoints.Count < 3)
            return new List<Vector3>();

        //inflate bounds a bit
        Bounds bounds = buildingObj.GetComponent<Collider>().bounds;
        float startY = bounds.min.y + 2.0f;
        float endY = bounds.max.y + 1.0f;

        //orbital ring generation
        //prepare centroid
        Vector3 centroid = GetCentroid(footprintPoints);
        lastcentroid = centroid;

        (List<Vector3> orbitRing, List<Vector3> orbitNormal) = GenerateOrbitRing(footprintPoints, centroid);
        StartToCamera(ref orbitRing, ref orbitNormal, centroid);

        if (missionType == MissionType.Vertical) {
            if (missionStyle == MissionStyle.Collision)
                return GenerateVertical(orbitRing, orbitNormal, centroid, startY, endY);
        }

        if (missionType == MissionType.Horizontal) {
            if (missionStyle == MissionStyle.Collision)
                return GenerateHorizontal(orbitRing, orbitNormal, centroid, startY, endY);
        }

        return new List<Vector3>();
    }

    private Vector3 GetCentroid(List<Vector3> footprint) {
        Vector3 centroid = Vector3.zero;
        foreach (var p in footprint)
            centroid += p;
        centroid /= footprint.Count;
        centroid.y = 0;
        return centroid;
    }

    private (List<Vector3> ring, List<Vector3> normals) GenerateOrbitRing(List<Vector3> footprint, Vector3 centroid) {
        List<Vector3> orbitRing = new List<Vector3>();
        List<Vector3> orbitNormal = new List<Vector3>();

        //recopy the footprint with point of max seglen apart
        List<Vector3> sepFootprint = new List<Vector3>();
        for (int i = 0; i < footprint.Count; i++) {
            Vector3 p1 = footprint[i];
            int k = i + 1;
            if (k >= footprint.Count)
                k = 0;
            Vector3 p2 = footprint[k];
            p1.y = 0;
            p2.y = 0;

            sepFootprint.Add(p1);
            float dist = Vector3.Distance(p1, p2);

            //for vertical missions use photo interval as segment
            float maxSeg = maxSegmentLen;
            if (missionType == MissionType.Vertical)
                maxSeg = photoInterval;

            //if any point is farther interpolate between
            if (dist > maxSeg) {
                int steps = Mathf.CeilToInt(dist / maxSeg);
                for (int j = 1; j < steps; j++) {
                    sepFootprint.Add(Vector3.Lerp(p1, p2, (float) j / steps));
                }
            }
        }

        float signedArea = 0f;
        for (int i = 0; i < sepFootprint.Count; i++) {
            Vector3 a = sepFootprint[i];
            Vector3 b = sepFootprint[(i + 1) % sepFootprint.Count];
            signedArea += (a.x * b.z) - (b.x * a.z);
        }
        bool isCCW = (signedArea > 0f);

        for (int i = 0; i < sepFootprint.Count; i++) {
            Vector3 curr = sepFootprint[i];
            Vector3 prev = sepFootprint[(i - 1 + sepFootprint.Count) % sepFootprint.Count];
            Vector3 next = sepFootprint[(i + 1) % sepFootprint.Count];

            //vectors of the walls
            Vector3 dirPrev = (curr - prev).normalized;
            Vector3 dirNext = (next - curr).normalized;

            Vector3 normPrev = isCCW ? new Vector3(dirPrev.z, 0, -dirPrev.x) : new Vector3(-dirPrev.z, 0, dirPrev.x);
            Vector3 normNext = isCCW ? new Vector3(dirNext.z, 0, -dirNext.x) : new Vector3(-dirNext.z, 0, dirNext.x);

            //average normal for the vertex
            Vector3 vertexNormal = (normPrev + normNext).normalized;
            if (vertexNormal == Vector3.zero)
                vertexNormal = normPrev;

            //move the offset of scandist
            Vector3 offsetPoint = curr + (vertexNormal * scanDistance);

            float minOrbitSpacing = (missionType == MissionType.Vertical) ? photoInterval * 0.8f : 0.1f;
            if (orbitRing.Count == 0 || Vector3.Distance(offsetPoint, orbitRing[orbitRing.Count - 1]) > minOrbitSpacing) {
                orbitRing.Add(offsetPoint);
                orbitNormal.Add(normNext);
            }
        }
        return (orbitRing, orbitNormal);
    }

    private List<Vector3> GenerateHorizontal(List<Vector3> orbitRing, List<Vector3> orbitNormal, Vector3 centroid, float startY, float endY) {
        //horizontal level generation
        List<Vector3> finalPath = new List<Vector3>();
        List<int> finalPathLevels = new List<int>();
        Vector3 lastPoint = Vector3.zero;
        bool isFirst = true;
        int levelIndex = 0;

        for (float currentY = startY; currentY <= endY; currentY += verticalStep) {
            for (int i = 0; i < orbitRing.Count; i++) {
                Vector3 pt = orbitRing[i];
                Vector3 propPos = new Vector3(pt.x, currentY, pt.z);
                Vector3 currCenter = new Vector3(centroid.x, currentY, centroid.z);
                Vector3 pushDir = (propPos - currCenter).normalized;
                Vector3 noCollisionPos = SolveCollision(propPos, pushDir);

                if (!isFirst && Vector3.Distance(lastPoint, noCollisionPos) < 0.2f)
                    continue;

                if (!isFirst) {
                    List<Vector3> sightPoints = SolveSightline(lastPoint, noCollisionPos, pushDir);
                    finalPath.AddRange(sightPoints);
                    for (int j = 0; j < sightPoints.Count; j++) {
                        helixNormals.Add(orbitNormal[i]);
                        finalPathLevels.Add(levelIndex);
                    }
                    lastPoint = sightPoints[sightPoints.Count - 1];
                } else {
                    finalPath.Add(noCollisionPos);
                    helixNormals.Add(orbitNormal[i]);
                    finalPathLevels.Add(levelIndex);
                    lastPoint = noCollisionPos;
                    isFirst = false;
                }
            }

            //build columns
            float nextY = currentY + verticalStep;
            if (nextY <= endY && finalPath.Count > 0) {
                //on the last orbit point
                Vector3 climbPoint = new Vector3(lastPoint.x, nextY, lastPoint.z);
                finalPath.Add(climbPoint);
                Vector3 lastNormal = Vector3.forward;

                if (helixNormals.Count > 0)
                    lastNormal = helixNormals[helixNormals.Count - 1];

                //column into curr level
                helixNormals.Add(lastNormal);
                finalPathLevels.Add(levelIndex);
                lastPoint = climbPoint;
            }

            levelIndex++;
        }

        VisualizePath(finalPath, finalPathLevels);
        return finalPath;
    }

    private void StartToCamera(ref List<Vector3> orbitRing, ref List<Vector3> orbitNormal, Vector3 centroid) {
        //rotate points so that closest to cam is first
        if (arcGISCamera != null) {
            Vector3 centroidFlat = new Vector3(centroid.x, 0, centroid.z);
            Vector3 camFlat = new Vector3(arcGISCamera.transform.position.x, 0, arcGISCamera.transform.position.z);
            Vector3 camDir = (camFlat - centroidFlat).normalized;

            float maxDot = float.MinValue;
            int startIndex = 0;
            for (int i = 0; i < orbitRing.Count; i++) {
                Vector3 pointDir = (orbitRing[i] - centroidFlat).normalized;
                float dot = Vector3.Dot(pointDir, camDir);
                if (dot > maxDot) {
                    maxDot = dot;
                    startIndex = i;
                }
            }
            orbitRing = RotateList(orbitRing, startIndex);
            orbitNormal = RotateList(orbitNormal, startIndex);
        }
    }

    private List<Vector3> GenerateVertical(List<Vector3> orbitRing, List<Vector3> orbitNormal, Vector3 centroid, float startY, float endY) {
        List<Vector3> finalPath = new List<Vector3>();
        Vector3 lastPoint = Vector3.zero;
        bool isFirst = true;
        bool goingUp = true; // first column always ascends

        for (int i = 0; i < orbitRing.Count; i++) {
            int normalIdx = Mathf.Min(i, orbitNormal.Count - 1);
            Vector3 pt = orbitRing[i];

            //determine col direction
            List<float> colY = new List<float>();
            if (goingUp) {
                colY.Add(startY);
                colY.Add(endY);
            } else {
                colY.Add(endY);
                colY.Add(startY);
            }


            foreach (float y in colY) {
                Vector3 propPos = new Vector3(pt.x, y, pt.z);
                Vector3 currCenter = new Vector3(centroid.x, y, centroid.z);
                Vector3 pushDir = (propPos - currCenter).normalized;
                Vector3 resolved = SolveCollision(propPos, pushDir);

                if (!isFirst && Vector3.Distance(lastPoint, resolved) < 0.2f)
                    continue;

                if (!isFirst) {
                    List<Vector3> via = SolveSightline(lastPoint, resolved, pushDir);
                    finalPath.AddRange(via);
                    for (int k = 0; k < via.Count; k++)
                        helixNormals.Add(orbitNormal[normalIdx]);
                    lastPoint = via[via.Count - 1];
                } else {
                    finalPath.Add(resolved);
                    helixNormals.Add(orbitNormal[normalIdx]);
                    lastPoint = resolved;
                    isFirst = false;
                }
            }

            goingUp = !goingUp; // alternate direction for next column
        }

        VisualizePath(finalPath);
        return finalPath;
    }

    private Vector3 SolveCollision(Vector3 targetpos, Vector3 pushDir) {
        Vector3 curr = targetpos;
        float pushed = 0.0f;
        float step = 0.5f;
        float maxHorizPush = 3.0f;

        //tries to push out until is not blocked
        while (IsPosBlocked(curr)) {
            curr += pushDir * step;
            pushed += step;

            //if cant be pushed out try to fly over
            if (pushed > maxHorizPush) {
                Debug.LogWarning("Max push exceeded, returning original position");
                return FindSafeAlt(targetpos);
            }
        }
        return curr;
    }

    private Vector3 FindSafeAlt(Vector3 pos) {
        Vector3 sky = new Vector3(pos.x, pos.y + 100.0f, pos.z);
        RaycastHit hit;

        //position cant be blocked from up
        if (Physics.Raycast(sky, Vector3.down, out hit, 200f, collisionLayer)) {
            //add drone dimension for safe flight
            float safeY = hit.point.y + droneRadius + 1.0f;

            safeY = Mathf.Max(safeY, pos.y);

            return new Vector3(pos.x, safeY, pos.z);
        }

        return pos;
    }

    private float GetHighestAlt(Vector3 prevPoint, Vector3 currPoint) {
        //save currently highest point
        float highest = Mathf.Max(prevPoint.y, currPoint.y);
        float dist = Vector3.Distance(prevPoint, currPoint);

        //round up max steps but not lower than 1
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.5f));

        for (int i = 1; i < steps; i++) {
            //interpolate current point
            float t = (float) i / steps;
            Vector3 pos = Vector3.Lerp(prevPoint, currPoint, t);

            //tries to find safe alt at the point
            float alt = FindSafeAlt(pos).y;
            if (alt > highest) {
                highest = alt;
            }
        }
        return highest;
    }

    private bool IsPosBlocked(Vector3 pos) {
        //checks if any objects are too close
        if (Physics.CheckSphere(pos, droneRadius, collisionLayer)) {
            return true;
        }

        //in case of being hidden inside or under cast from sky
        Vector3 sky = new Vector3(pos.x, pos.y + 100.0f, pos.z);
        if (Physics.Linecast(sky, pos, collisionLayer)) {
            return true;
        }

        return false;
    }

    public List<Vector3> SolveSightline(Vector3 prevPoint, Vector3 currPoint, Vector3 pushDir) {
        List<Vector3> segPoints = new List<Vector3>();
        RaycastHit hit;

        //if one of casts hits, proceeds to overfly obsticle
        Vector3 dir = currPoint - prevPoint;
        float dist = dir.magnitude;
        if (dist > 0.1f && (Physics.SphereCast(prevPoint, droneRadius, dir.normalized, out hit, dist, collisionLayer) || Physics.Linecast(prevPoint, currPoint, collisionLayer))) {
            //get safe alt to fly over
            float safeAlt = GetHighestAlt(prevPoint, currPoint);
            Vector3 risePoint = new Vector3(prevPoint.x, safeAlt, prevPoint.z);

            //add point to start fly over
            if (Vector3.Distance(prevPoint, risePoint) > 0.1f) {
                segPoints.Add(risePoint);
            }
            //add drop point above in case longer path
            Vector3 dropPoint = new Vector3(currPoint.x, safeAlt, currPoint.z);
            if (Vector3.Distance(risePoint, dropPoint) > 0.1f) {
                segPoints.Add(dropPoint);
            }
            //add final point
            if (Vector3.Distance(dropPoint, currPoint) > 0.1f) {
                segPoints.Add(currPoint);
            }
        } else {
            segPoints.Add(currPoint);
        }

        return segPoints;
    }

    public void ReconnectLevelPath(List<GameObject> remaining) {
        Debug.LogWarning("Running level reconnect");
        if (remaining == null || remaining.Count == 0)
            return;

        //sort all waypoints
        remaining.Sort((a, b) => GetWaypointIdx(a).CompareTo(GetWaypointIdx(b)));

        //sort by orbit level
        Dictionary<int, List<GameObject>> sLvl = new Dictionary<int, List<GameObject>>();
        foreach (GameObject wp in remaining) {
            int lvl = 0;
            if (waypointLevel.ContainsKey(wp))
                lvl = waypointLevel[wp];
            if (!sLvl.ContainsKey(lvl))
                sLvl[lvl] = new List<GameObject>();
            sLvl[lvl].Add(wp);
        }

        //sort ascending lvls
        List<int> lvls = new List<int>(sLvl.Keys);
        lvls.Sort();

        //sorting into snake
        List<GameObject> neworder = new List<GameObject>();
        Vector3 lastPos = Vector3.zero;
        bool firstLvl = true;
        bool swap = false;

        foreach (int lvl in lvls) {
            List<GameObject> lvlWPs = sLvl[lvl];
            int startIdx = 0;

            if (!firstLvl) {
                float minDist = float.MaxValue;
                for (int i = 0; i < lvlWPs.Count; i++) {
                    float dist = Vector3.Distance(lastPos, lvlWPs[i].transform.position);
                    if (dist < minDist) {
                        minDist = dist;
                        startIdx = i;
                    }
                }
            }

            //trough first level to the end
            //swapping the direction of every level
            for (int i = 0; i < lvlWPs.Count; i++) {
                int idx = 0;
                if (swap)
                    idx = (startIdx - i + lvlWPs.Count) % lvlWPs.Count;
                else
                    idx = (startIdx + i) % lvlWPs.Count;
                neworder.Add(lvlWPs[idx]);
            }

            lastPos = neworder[neworder.Count - 1].transform.position;
            firstLvl = false;
            swap = !swap;   //next swap
        }

        //rename waypoints to fit new order
        for (int i = 0; i < neworder.Count; i++) {
            neworder[i].name = $"WP_{i}";
        }

        //destroy all old tubes from remaining WPs
        RemoveTubes(neworder);

        //create new tubes between consecutive pairs in neworder
        for (int i = 0; i < neworder.Count - 1; i++) {
            AddTube(neworder[i], neworder[i + 1]);
        }
    }

    public List<GPSWaypoint> ConvertToGPSCoordinates(List<Vector3> unityPath, List<Vector3> normals) {
        List<GPSWaypoint> gpsPath = new List<GPSWaypoint>();

        if (mapComponent == null) {
            Debug.LogError("MissionGenerator no arcgis map reference");
            return gpsPath;
        }

        //convert each point
        for (int i = 0; i < unityPath.Count; i++) {
            Vector3 point = unityPath[i];

            //relative unity coords to gps coords
            ArcGISPoint geoPos = mapComponent.EngineToGeographic(point);

            //setup new waypoint
            GPSWaypoint wp = new GPSWaypoint();
            wp.latitude = geoPos.Y;
            wp.longitude = geoPos.X;
            wp.altitude = geoPos.Z;
            wp.speed = flightSpeed;

            //prepare heading to waypoint
            //or calculate from centroid
            if (normals != null && i < normals.Count) {
                Vector3 neg = -normals[i];
                float heading = Mathf.Atan2(neg.x, neg.z) * Mathf.Rad2Deg;
                if (heading < 0)
                    heading += 360;
                wp.heading = heading;
            } else if (lastcentroid != Vector3.zero) {
                float vectx = unityPath[i].x - lastcentroid.x;
                float vectz = unityPath[i].z - lastcentroid.z;
                Vector3 dir = new Vector3(vectx, 0, vectz).normalized;
                float heading = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                if (heading < 0)
                    heading += 360;
                wp.heading = heading;
            }

            gpsPath.Add(wp);
        }

        return gpsPath;
    }

    public void VisualizePath(List<Vector3> path, List<int> levelData = null) {
        if (path.Count == 0)
            return;

        int currlvl = 0;
        float lvlY = path[0].y;

        if (use3DTubes)
            lineRenderer.enabled = false;
        else {
            lineRenderer.positionCount = path.Count;
            lineRenderer.SetPositions(path.ToArray());
        }

        //creates waypoints and tubes between
        GameObject prevP = null;
        for (int i = 0; i < path.Count; i++) {
            Vector3 currentPos = path[i];

            if (levelData != null && i < levelData.Count) {
                currlvl = levelData[i];
            } else if (i > 0 && path[i].y > lvlY + 0.1f) {
                currlvl++;
                lvlY = path[i].y;
            }

            //create waypoint
            GameObject currP = CreateWaypoint(currentPos, i, path.Count, currlvl);
            if (currP == null)
                return;
            tubeMap[currP] = new List<GameObject>();

            if (use3DTubes && i > 0) {
                //tubes bewteen
                GameObject tube = CreateTube(path[i - 1], currentPos, i - 1);

                tubeMap[currP].Add(tube);
                tubeMap[prevP].Add(tube);
            }
            prevP = currP;
        }
    }

    private GameObject CreateWaypoint(Vector3 pos, int index, int pathCount, int level = 0) {
        GameObject wpObj = null;

        if (waypointPrefab != null) {
            //instantiate from my prefab
            wpObj = Instantiate(waypointPrefab, pos, Quaternion.identity);
        } else {
            Toast.call.Show("Waypoint prefab missing!", 3.0f, true);
            return null;
        }

        //set parameters and layer
        wpObj.name = $"WP_{index}";
        wpObj.transform.position = pos;
        wpObj.transform.localScale = Vector3.one * waypointSize;
        int layerIndex = LayerMask.NameToLayer("Mission");
        wpObj.layer = (layerIndex != -1) ? layerIndex : 0;
        spawnedObjects.Add(wpObj);

        if (pathCount <= 0)
            return wpObj;

        Texture2D wp = null;
        if (index == 0) {
            wp = startTexture;
        } else if (index == pathCount - 1) {
            wp = finishTexture;
        }

        //apply texture for first and last wps
        if (wp != null) {
            MeshRenderer mr = wpObj.GetComponent<MeshRenderer>();
            if (mr != null) {
                mr.material.mainTexture = wp;
                mr.material.color = Color.white;
            }
            wpObj.transform.localScale = Vector3.one * waypointSize * 1.5f;
        }

        waypointLevel[wpObj] = level;
        return wpObj;
    }

    private GameObject CreateTube(Vector3 start, Vector3 end, int index) {
        //creates cylinder
        GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tube.name = "Tube_Segment_" + index;
        Collider col = tube.GetComponent<Collider>();

        if (col != null) {
            col.enabled = false;
            col.isTrigger = false;
        } else {
            Debug.LogWarning("no collider!");
        }

        //set layer so it renders infornt of buildings
        tube.layer = layerMission;

        //create new renderer for color a material
        MeshRenderer renderer = tube.GetComponent<MeshRenderer>();
        renderer.material = new Material(tubeMaterial);

        //tube only uses center and end as a vector to look at
        Vector3 centerPos = (start + end) / 2f;
        float distance = Vector3.Distance(start, end);
        tube.transform.position = centerPos;
        tube.transform.LookAt(end);
        tube.transform.Rotate(90, 0, 0);
        tube.transform.localScale = new Vector3(tubeThickness, distance / 2f, tubeThickness);
        spawnedObjects.Add(tube);

        return tube;
    }

    public void AddTube(GameObject wp1, GameObject wp2) {
        //creates tube between two wps, adds to map
        GameObject tube = CreateTube(wp1.transform.position, wp2.transform.position, -1);
        if (!tubeMap.ContainsKey(wp1))
            tubeMap[wp1] = new List<GameObject>();
        tubeMap[wp1].Add(tube);

        if (!tubeMap.ContainsKey(wp2))
            tubeMap[wp2] = new List<GameObject>();
        tubeMap[wp2].Add(tube);
    }

    public void AddTube(GameObject wp1, GameObject wp2, Vector3 from, Vector3 to) {
        GameObject tube = CreateTube(from, to, -1);
        if (!tubeMap.ContainsKey(wp1))
            tubeMap[wp1] = new List<GameObject>();
        tubeMap[wp1].Add(tube);
        if (!tubeMap.ContainsKey(wp2))
            tubeMap[wp2] = new List<GameObject>();
        tubeMap[wp2].Add(tube);
    }

    public void UpdateLineRenderer(List<Vector3> positions) {
        lineRenderer.positionCount = positions.Count;
        lineRenderer.SetPositions(positions.ToArray());
    }

    public float CalculateWidthCoverage() {
        //percentage of camera coverage horizontally
        float view = (scanDistance * senzWidth) / focalLength;
        float cov = (view - photoInterval) / view;
        return cov * 100.0f;
    }

    public float CalculateHeightCoverage() {
        //percentage of camera coverage vertically
        float view = (scanDistance * senzHeight) / focalLength;
        float cov = (view - verticalStep) / view;
        return cov * 100.0f;
    }

    public float CalculatePhotoDistance(float targetOverlap = 0.8f) {
        //percentage width of camera to shoot new photo
        float viewWidth = (scanDistance * senzWidth) / focalLength;
        return viewWidth * (1f - targetOverlap);
    }

    public void RecalculateSteps() {
        float footprintH = scanDistance * (senzHeight / focalLength);
        float footprintW = scanDistance * (senzWidth / focalLength);
        verticalStep = Mathf.Max(footprintH * (1f - heightOverlap), 0.3f);
        photoInterval = Mathf.Max(footprintW * (1f - widthOverlap), 0.5f);
        //maxSegmentLen = photoInterval;
    }

    public void ClearPath() {
        //clearing all mission variables/objects
        lineRenderer.positionCount = 0;
        foreach (var obj in spawnedObjects) {
            if (obj != null)
                Destroy(obj);
        }
        spawnedObjects.Clear();
        waypointLevel.Clear();
        tubeMap.Clear();
    }

    public void RemoveTubes(List<GameObject> waypoints, List<GameObject> rem = null) {
        var tubs = new HashSet<GameObject>();

        foreach (GameObject wp in waypoints) {
            if (wp == null || !tubeMap.ContainsKey(wp))
                continue;
            foreach (GameObject tube in tubeMap[wp]) {
                if (tube != null)
                    tubs.Add(tube);
            }
            tubeMap.Remove(wp);
        }

        if (rem != null) {
            foreach (GameObject wp in rem) {
                if (tubeMap.ContainsKey(wp))
                    tubeMap[wp].RemoveAll(tubs.Contains);
            }
        }

        foreach (GameObject tube in tubs) {
            spawnedObjects.Remove(tube);
            Destroy(tube);
        }
    }

    public void RemoveWaypoint(List<GameObject> waypoints) {
        foreach (GameObject wp in waypoints) {
            if (wp == null)
                continue;

            tubeMap.Remove(wp);
            spawnedObjects.Remove(wp);
            Destroy(wp);
        }
    }

    public void RemoveNormalsIdx(List<int> indexes) {
        if (helixNormals == null)
            return;
        foreach (int index in indexes.OrderByDescending(i => i)) {
            if (index < helixNormals.Count)
                helixNormals.RemoveAt(index);
        }
    }

    public bool HasMission() {
        return spawnedObjects.Count > 0;
    }

    public List<GameObject> GetMissionWaypoints() {
        List<GameObject> waypoints = new List<GameObject>();
        foreach (GameObject wp in tubeMap.Keys) {
            waypoints.Add(wp);
        }
        return waypoints;
    }

    public Vector3 GetMissionCenter(List<Vector3> points) {
        if (points == null || points.Count == 0)
            return Vector3.zero;

        //simple average of points
        Vector3 sum = Vector3.zero;
        int count = points.Count;
        foreach (Vector3 p in points) {
            sum += p;
        }
        return sum / count;
    }

    public List<GameObject> GetWaypointTubes(GameObject wp) {
        if (tubeMap.ContainsKey(wp))
            return tubeMap[wp];
        return new List<GameObject>();
    }

    public int GetWaypointLevel(GameObject wp) {
        if (waypointLevel.ContainsKey(wp))
            return waypointLevel[wp];
        return 0;
    }

    public Vector3 GetNormal(int idx) {
        if (helixNormals != null && idx >= 0 && idx < helixNormals.Count)
            return helixNormals[idx];
        return Vector3.zero;
    }

    public static int GetWaypointIdx(GameObject wp) {
        int.TryParse(wp.name.Replace("WP_", ""), out int idx);
        return idx;
    }

    public List<Vector3> GetMissionNormals(List<Vector3> points, Vector3 center) {
        List<Vector3> normals = new List<Vector3>();
        if (points == null || points.Count == 0)
            return normals;

        for (int i = 0; i < points.Count; i++) {
            int next = (i + 1) % points.Count;
            Vector3 dir = points[next] - points[i];
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) {
                Vector3 fall = points[i] - center;
                fall.y = 0;
                if (fall.sqrMagnitude < 0.001f) {
                    normals.Add(Vector3.forward);
                } else {
                    normals.Add(fall.normalized);
                }
                continue;
            }
            dir.Normalize();
            Vector3 perp = new Vector3(dir.z, 0, -dir.x);
            Vector3 outward = points[i] - center;
            outward.y = 0;
            if (Vector3.Dot(perp, outward) < 0)
                perp = -perp;
            normals.Add(perp);
        }

        return normals;
    }

    private List<T> RotateList<T>(List<T> list, int offset) {
        if (offset == 0 || list.Count == 0)
            return list;

        //set new list of capacity
        List<T> getRotatedI = new List<T>(list.Count);
        //rotate by offset
        for (int i = 0; i < list.Count; i++)
            getRotatedI.Add(list[(i + offset) % list.Count]);

        return getRotatedI;
    }
}
