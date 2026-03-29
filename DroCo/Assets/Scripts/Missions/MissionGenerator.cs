using System.Collections.Generic;
using UnityEngine;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Geometry;
using UnityEngine.UI;
using Unity.VisualScripting;
using TMPro;
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
    //public GameObject waypointUIPrefab;
    public float waypointSize = 0.3f;
    public float flightSpeed = 5.0f;

    [Header("Icons")]
    public Sprite mapActiveIcon;
    public Sprite mapIcon;

    [Header("Collision")]
    public float droneRadius = 0.5f;
    public LayerMask collisionLayer;
    public float maxPush = 15.0f;

    [Header("Cam")]
    public Camera arcGISCamera;
    public float senzWidth = 6.17f;
    public float senzHeight = 4.55f;
    public float focalLength = 4.67f;

    private LineRenderer lineRenderer;
    public List<GameObject> spawnedObjects = new List<GameObject>();
    public Dictionary<GameObject, List<GameObject>> tubeMap = new Dictionary<GameObject, List<GameObject>>();
    public List<Vector3> helixNormals = new List<Vector3>();
    public Vector3 lastcentroid = Vector3.zero;

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
        //reset previous mission
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
        List<Vector3> orbitRing = new List<Vector3>();
        List<Vector3> orbitNormal = new List<Vector3>();
        Vector3 centroid = Vector3.zero;
        foreach (var p in footprintPoints)
            centroid += p;
        centroid /= footprintPoints.Count;
        centroid.y = 0;
        lastcentroid = centroid;

        //recopy the footprint with point of max seglen apart
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

            //if any point is farther interpolate between
            if (dist > maxSeg) {
                int steps = Mathf.CeilToInt(dist / maxSeg);
                for (int j = 1; j < steps; j++) {
                    sepFootprint.Add(Vector3.Lerp(p1, p2, (float) j / steps));
                }
            }
        }

        //
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

            //perpendicular vects
            /*
            Vector3 normPrev = new Vector3(-dirPrev.z, 0, dirPrev.x);
            if (Vector3.Dot(normPrev, curr - centroid) < 0)
                normPrev = -normPrev; //point out always

            Vector3 normNext = new Vector3(-dirNext.z, 0, dirNext.x);
            if (Vector3.Dot(normNext, curr - centroid) < 0)
                normNext = -normNext;
            */

            Vector3 normPrev = isCCW ? new Vector3(dirPrev.z, 0, -dirPrev.x) : new Vector3(-dirPrev.z, 0, dirPrev.x);
            Vector3 normNext = isCCW ? new Vector3(dirNext.z, 0, -dirNext.x) : new Vector3(-dirNext.z, 0, dirNext.x);


            //average normal for the vertex
            Vector3 vertexNormal = (normPrev + normNext).normalized;
            if (vertexNormal == Vector3.zero)
                vertexNormal = normPrev;

            //move the offset of scandist
            Vector3 offsetPoint = curr + (vertexNormal * scanDistance);

            if (orbitRing.Count == 0 || Vector3.Distance(offsetPoint, orbitRing[orbitRing.Count - 1]) > 0.1f) {
                orbitRing.Add(offsetPoint);
                //orbitNormal.Add(vertexNormal);
                orbitNormal.Add(normNext);
            }


            //concave detection via cross product
            /*
            float cross = dirPrev.x * dirNext.z - dirPrev.z * dirNext.x;
            bool isConcave = isCCW ? (cross < -0.01f) : (cross > 0.01f);

            if (isConcave) {
                Vector3 pt1 = curr + normPrev * scanDistance;
                // přidat: střed kapsy
                Vector3 ptMid = curr + (normPrev + normNext).normalized * scanDistance;
                Vector3 pt2 = curr + normNext * scanDistance;

                if (orbitRing.Count == 0 || Vector3.Distance(pt1, orbitRing[orbitRing.Count - 1]) > 0.1f) {
                    orbitRing.Add(pt1);
                    orbitNormal.Add(normPrev);
                }
                if (Vector3.Distance(ptMid, pt1) > 0.1f) {
                    orbitRing.Add(ptMid);
                    orbitNormal.Add((normPrev + normNext).normalized);
                }
                if (Vector3.Distance(pt2, ptMid) > 0.1f) {
                    orbitRing.Add(pt2);
                    orbitNormal.Add(normNext);
                }
            } else {
                // convex corner: original behavior - averaged normal
                Vector3 vertexNormal = (normPrev + normNext).normalized;
                if (vertexNormal == Vector3.zero)
                    vertexNormal = normPrev;

                Vector3 offsetPoint = curr + (vertexNormal * scanDistance);

                if (orbitRing.Count == 0 || Vector3.Distance(offsetPoint, orbitRing[orbitRing.Count - 1]) > 0.1f) {
                    orbitRing.Add(offsetPoint);
                    orbitNormal.Add(vertexNormal);
                }
            }
            */
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

                //break if exceeded building height
                if (actualY > endY)
                    break;

                //prepare current position and push direction for collisions
                Vector3 pt = orbitRing[i];
                Vector3 propPos = new Vector3(pt.x, actualY, pt.z);
                Vector3 currCenter = new Vector3(centroid.x, actualY, centroid.z);
                Vector3 pushDir = (propPos - currCenter).normalized;
                //solve collisions if any
                Vector3 noCollisionPos = SolveCollision(propPos, pushDir);

                if (!isFirst && Vector3.Distance(lastPoint, noCollisionPos) < 0.2f) {
                    continue;
                }

                //solve sightline for point to not clip through walls
                if (!isFirst) {
                    List<Vector3> sightPoints = SolveSightline(lastPoint, noCollisionPos, pushDir);
                    finalPath.AddRange(sightPoints);
                    for (int j = 0; j < sightPoints.Count; j++) {
                        helixNormals.Add(orbitNormal[i]);
                    }
                    lastPoint = sightPoints[sightPoints.Count - 1];
                } else {
                    finalPath.Add(noCollisionPos);
                    helixNormals.Add(orbitNormal[i]);
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

        //tries to push out until is not blocked
        while (isPosBlocked(curr)) {
            curr += pushDir * step;
            pushed += step;

            //if cant be pushed out try to fly over
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

        //position cant be blocked from up
        if (Physics.Raycast(sky, Vector3.down, out hit, 200f, collisionLayer)) {
            //add drone dimension for safe flight
            float safeY = hit.point.y + droneRadius + 1.0f;

            safeY = Mathf.Max(safeY, pos.y);

            return new Vector3(pos.x, safeY, pos.z);
        }

        return pos;
    }

    private float getHighestAlt(Vector3 prevPoint, Vector3 currPoint) {
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
            float alt = findSafeAlt(pos).y;
            if (alt > highest) {
                highest = alt;
            }
        }
        return highest;
    }

    private bool isPosBlocked(Vector3 pos) {
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
        Vector3 finPoint = currPoint;
        RaycastHit hit;
        //float pushed = 0.0f;
        //float step = 0.5f;

        //if one of casts hits, proceeds to overfly obsticle
        Vector3 dir = finPoint - prevPoint;
        float dist = dir.magnitude;
        if (dist > 0.1f && (Physics.SphereCast(prevPoint, droneRadius, dir.normalized, out hit, dist, collisionLayer) || Physics.Linecast(prevPoint, currPoint, collisionLayer))) {
            //get safe alt to fly over
            float safeAlt = getHighestAlt(prevPoint, currPoint);
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

    public void VisualizePath(List<Vector3> path) {
        if (path.Count == 0)
            return;

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

            //create waypoint
            GameObject currP = CreateWaypoint(currentPos, i, path.Count);
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

    private GameObject CreateWaypoint(Vector3 pos, int index, int pathCount) {
        GameObject wpObj = null;

        if (waypointPrefab != null) {
            //instantiate from my prefab
            wpObj = Instantiate(waypointPrefab, pos, Quaternion.identity);
        } else {
            Toast.call.Show("Waypoint prefab missing!", 3.0f, true);
            return null;
            //fallback for universal waypoint
            //TODO this might delete later
            /*
            wpObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var renderer = wpObj.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader)
                shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = Color.yellow;
            renderer.material = mat;
            */
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

        return wpObj;
    }

    private GameObject CreateTube(Vector3 start, Vector3 end, int index) {
        //creates cylinder
        GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tube.name = "Tube_Segment_" + index;
        Collider col = tube.GetComponent<Collider>();

        if (col != null) {
            //TODO will set to off later
            col.enabled = false;
            col.isTrigger = false;
        } else {
            Debug.LogWarning("no collider!");
        }

        //set layer so it renders infornt of buildings
        int layerIndex = LayerMask.NameToLayer("Mission");
        tube.layer = (layerIndex != -1) ? layerIndex : 0;

        //create new renderer for color a material
        var renderer = tube.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader)
            shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = pathColor;
        mat.SetFloat("_Smoothness", 0.5f);
        renderer.material = mat;

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
        spawnedObjects.Add(tube);
    }

    public void UpdateLineRenderer(List<Vector3> positions) {
        lineRenderer.positionCount = positions.Count;
        lineRenderer.SetPositions(positions.ToArray());
    }

    public float calculateWidthCoverage() {
        //percentage of camera coverage horizontally
        float view = (scanDistance * senzWidth) / focalLength;
        float cov = (view - maxSegmentLen) / view;
        return cov * 100.0f;
    }

    public float calculateHeightCoverage() {
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

    public void ClearPath() {
        //clearing all mission variables/objects
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

    public List<Vector3> GetMissionNormals(List<Vector3> points, Vector3 center) {
        List<Vector3> normals = new List<Vector3>();
        if (points == null || points.Count == 0)
            return normals;
        /*
        foreach (Vector3 p in points) {
            Vector3 dir = (center - p);
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) {
                normals.Add(Vector3.forward);
                continue;
            }
            dir.Normalize();

            if (Physics.Raycast(p, dir, out RaycastHit hit, 100f)) {
                Vector3 wallNormal = -hit.normal;
                normals.Add(wallNormal);
            } else {
                normals.Add(-dir);
            }
        }
        */

        for (int i = 0; i < points.Count; i++) {
            int next = (i + 1) % points.Count;
            Vector3 dir = points[next] - points[i];
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) {
                normals.Add(Vector3.forward);
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
