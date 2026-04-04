using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
//using System.Windows.Input;
using Esri.GameEngine.Geometry;
using System.Linq;
//using UnityEngine.TestTools.Constraints;

public class MissionController : MonoBehaviour {

    [Header("Reference")]
    public MissionGenerator generator;
    public DroneManager droneManager;
    public Settings settings;
    public Navigator navigator;

    [Header("Starting Point")]
    public double startLat = 49.226015;
    public double startLon = 16.597071;
    public double startAlt = 250.0;

    [Header("Scan Parameters")]
    public float paramMaxHeight = 50f;
    public float paramMinHeight = 10f;
    public float paramOverlap = 0.5f;

    public MissionData currentMission;
    //TODO delete later
    //public List<Vector3> curentMissionNormals;
    public List<Vector3> currentMissionSticks;
    public Vector3 missionCenter;
    private GameObject currBuilding;
    private List<Vector3> currFootprint;
    //private List<Vector3> deletedPositions = new List<Vector3>();
    //private bool redeleting = false;
    public bool autoConnect = false;
    private bool snakeMode = false;

    void Update() {
        if (droneManager == null)
            return;

        Drone drone = droneManager.GetFirstDrone();
        if (drone == null)
            return;

        if (drone.FlightData == null)
            return;
        if (drone.FlightData.gps == null)
            return;
        startLat = drone.FlightData.gps.latitude;
        startLon = drone.FlightData.gps.longitude;
        startAlt = drone.FlightData.altitude;
    }

    public void PrepareMission(GameObject building, List<Vector3> footprint) {
        Debug.Log("PREPARING MISSION...");

        //generate path
        List<Vector3> rawHelixUnity = generator.GenerateScanPath(building, footprint);

        if (rawHelixUnity == null || rawHelixUnity.Count == 0) {
            Debug.LogError("Generator returned no path!");
            return;
        }

        //converting to gps coords
        //put in point structure
        List<GPSWaypoint> gpsHelix = generator.ConvertToGPSCoordinates(rawHelixUnity, generator.helixNormals);

        //put in complex structure
        MissionData complexMission = new MissionData();
        complexMission.route = new Route();
        complexMission.route.name = "helix scan";
        complexMission.route.segments = new List<Segment>();

        //one mission segment
        Segment scanSegment = new Segment();
        scanSegment.type = "scan"; //whatever type we want to use

        //parameters
        scanSegment.parameters = new Parameters();
        scanSegment.parameters.maxHeight = paramMaxHeight;
        scanSegment.parameters.minHeight = paramMinHeight;
        scanSegment.parameters.overlapForward = paramOverlap;
        scanSegment.parameters.overlapSide = paramOverlap;
        scanSegment.parameters.scanDistance = generator.scanDistance;
        scanSegment.parameters.scanPattern = "Helix";

        //multipoint segment
        scanSegment.multipoint = new MultiPoint();
        scanSegment.multipoint.points = new List<Point>();

        //add start to path
        AddPointToSegment(scanSegment, startLat, startLon, startAlt);

        //then we add the generated points
        foreach (var wp in gpsHelix) {
            AddPointToSegment(scanSegment, wp.latitude, wp.longitude, wp.altitude, wp.speed, wp.heading, wp.gimbal_pitch, wp.gimbal_yaw);
        }

        //add end to path
        AddPointToSegment(scanSegment, startLat, startLon, startAlt);

        //finalize mission structure
        complexMission.route.segments.Add(scanSegment);

        //sending over network
        //SendMissionToNetwork(complexMission);
        currentMission = complexMission;
    }

    private void UpdateMissionFromWaypoints() {
        List<GameObject> allwps = generator.GetMissionWaypoints();

        //list of positions
        List<Vector3> updatedPath = new List<Vector3>();
        foreach (GameObject wp in allwps) {
            updatedPath.Add(wp.transform.position);
        }

        //update current mission for sticks
        currentMissionSticks = updatedPath;

        //gps conversion
        List<GPSWaypoint> updatedGPS = generator.ConvertToGPSCoordinates(updatedPath, null);

        //back to currmission
        currentMission.route.segments[0].multipoint.points.Clear();

        AddPointToSegment(currentMission.route.segments[0], startLat, startLon, startAlt);

        foreach (GPSWaypoint gps in updatedGPS) {
            AddPointToSegment(currentMission.route.segments[0], gps.latitude, gps.longitude, gps.altitude, gps.speed, gps.heading, gps.gimbal_pitch, gps.gimbal_yaw);
        }

        AddPointToSegment(currentMission.route.segments[0], startLat, startLon, startAlt);
    }

    private void AddPointToSegment(Segment segment, double lat, double lon, double alt, float speed = 5.0f, float heading = 0.0f, float gimbalPitch = -45.0f, float gimbalYaw = 0.0f) {
        Point p = new Point();
        p.latitude = lat;
        p.longitude = lon;
        p.altitude = alt;
        p.altitudeType = "AMSL";
        p.speed = speed;
        p.heading = heading;
        p.gimbal_pitch = gimbalPitch;
        p.gimbal_yaw = gimbalYaw;
        segment.multipoint.points.Add(p);
    }

    private void RegenerateSnake() {
        if (currBuilding == null || currFootprint == null || currFootprint.Count < 2)
            return;

        List<GameObject> allWPs = generator.GetMissionWaypoints();
        if (allWPs == null || allWPs.Count == 0)
            return;

        allWPs.Sort((a, b) => MissionGenerator.GetWaypointIdx(a).CompareTo(MissionGenerator.GetWaypointIdx(b)));

        float newScanDist = generator.scanDistance;
        Vector3 footprintCenter = generator.GetMissionCenter(currFootprint);
        footprintCenter.y = 0;

        foreach (GameObject wp in allWPs) {
            Vector3 pos = new Vector3(wp.transform.position.x, 0, wp.transform.position.z);

            float minDist = float.MaxValue;
            Vector3 bestProjection = pos;
            Vector3 bestNormal = Vector3.forward;

            for (int i = 0; i < currFootprint.Count; i++) {
                Vector3 A = new Vector3(currFootprint[i].x, 0, currFootprint[i].z);
                Vector3 B = new Vector3(currFootprint[(i + 1) % currFootprint.Count].x, 0, currFootprint[(i + 1) % currFootprint.Count].z);

                Vector3 edge = B - A;
                float edgeLen = edge.magnitude;
                if (edgeLen < 0.001f)
                    continue;
                Vector3 edgeDir = edge / edgeLen;

                float dot = Vector3.Dot(pos - A, edgeDir);
                float t = Mathf.Clamp01(dot / edgeLen);
                Vector3 projection = A + edgeDir * (t * edgeLen);

                float dist = Vector3.Distance(pos, projection);
                if (dist < minDist) {
                    minDist = dist;
                    bestProjection = projection;
                    // kolmice na hranu v XZ rovine
                    Vector3 normal = new Vector3(edgeDir.z, 0, -edgeDir.x);
                    // orientuj ven od stredu
                    if (Vector3.Dot(bestProjection - footprintCenter, normal) < 0)
                        normal = -normal;
                    bestNormal = normal;
                }
            }

            wp.transform.position = new Vector3(
                bestProjection.x + bestNormal.x * newScanDist,
                wp.transform.position.y,
                bestProjection.z + bestNormal.z * newScanDist
            );
        }

        generator.RecalculateSteps();
        Collider col = currBuilding.GetComponent<Collider>();
        float baseY = col != null ? col.bounds.min.y + 2.0f : allWPs[0].transform.position.y;
        float maxY = col != null ? col.bounds.max.y + 1.0f : float.MaxValue;

        Dictionary<int, List<GameObject>> byLevel = new Dictionary<int, List<GameObject>>();
        foreach (GameObject wp in allWPs) {
            int lvl = generator.GetWaypointLevel(wp);
            if (!byLevel.ContainsKey(lvl))
                byLevel[lvl] = new List<GameObject>();
            byLevel[lvl].Add(wp);
        }
        List<int> levels = new List<int>(byLevel.Keys);
        levels.Sort();
        for (int i = 0; i < levels.Count; i++) {
            float newY = Mathf.Min(baseY + i * generator.verticalStep, maxY);
            foreach (GameObject wp in byLevel[levels[i]]) {
                Vector3 p = wp.transform.position;
                wp.transform.position = new Vector3(p.x, newY, p.z);
            }
        }

        /*
        HashSet<GameObject> tubesToDestroy = new HashSet<GameObject>();
        foreach (GameObject wp in allWPs) {
            if (!generator.tubeMap.ContainsKey(wp))
                continue;
            foreach (GameObject tube in generator.tubeMap[wp])
                if (tube != null)
                    tubesToDestroy.Add(tube);
            generator.tubeMap[wp].Clear();
        }
        foreach (GameObject tube in tubesToDestroy) {
            generator.spawnedObjects.Remove(tube);
            Destroy(tube);
        }
        */
        generator.RemoveTubes(allWPs);

        if (generator.use3DTubes) {
            for (int i = 0; i < allWPs.Count - 1; i++)
                generator.AddTube(allWPs[i], allWPs[i + 1]);
        } else {
            generator.UpdateLineRenderer(allWPs.Select(wp => wp.transform.position).ToList());
        }

        snakeMode = true;
    }

    public bool Regenerate() {
        if (currBuilding == null || currFootprint == null)
            return false;

        if (snakeMode) {
            RegenerateSnake();
            return false;
        }

        //generator updates path on parameter change
        generator.ClearPath();
        List<Vector3> path = generator.GenerateScanPath(currBuilding, currFootprint);
        if (path == null || path.Count == 0)
            return false;


        PrepareMission(currBuilding, currFootprint);

        return true;
    }

    private void SendMissionToNetwork(MissionData dataStructure) {
        if (WebSocketServer.Instance == null) {
            Debug.LogError("WebSocketServer not running!");
            return;
        }

        //pack the struct into wrapper
        NetworkWrapper msg = new NetworkWrapper();
        msg.type = "mission_upload";
        msg.data = dataStructure;

        //srialize to json
        string json = JsonConvert.SerializeObject(msg);

        //send to all clients
        WebSocketServer.Instance.BroadcastToAll(json);
    }

    public void MissionStart() {
        //loads mission into drone over network in waypoint mission
        //starts mission with virtual sticks navigator takes control
        if (currentMission != null) {
            UpdateMissionFromWaypoints();
            if (settings.isWaypointMission) {
                SendMissionToNetwork(currentMission);
            } else {
                List<Vector3> normals = generator.GetMissionNormals(currentMissionSticks, generator.GetMissionCenter(currentMissionSticks));
                navigator.StartMission("", currentMissionSticks, normals, generator.photoInterval);
            }
        } else {
            Debug.LogError("No mission ready to start. Try selecting a building first.");
        }
    }

    public void MissionStop() {
        //use controller command to stop with virtual sticks
        //use json command in waypoint mission to stop
        if (!settings.isWaypointMission) {
            navigator.StopDrone();
        } else {
            string stop = "{\"type\":\"stop_mission\",\"data\":{}}";
            WebSocketServer.Instance.BroadcastToAll(stop);
            Toast.call.Show("Mission stop requested!", 2f, true);
        }
    }

    public string SaveMission(string name = "none") {
        if (currentMission == null)
            return null;

        //update mission
        UpdateMissionFromWaypoints();
        currentMission.route.name = name;

        //prepare model to save
        Collider col = currBuilding.GetComponent<Collider>();
        BuildingInfo info = new BuildingInfo();
        info.name = name;

        //preserving global height which could be different in different map centering
        Bounds b = col.bounds;
        ArcGISPoint down = generator.mapComponent.EngineToGeographic(new Vector3(b.center.x, b.min.y, b.center.z));
        ArcGISPoint top = generator.mapComponent.EngineToGeographic(new Vector3(b.center.x, b.max.y, b.center.z));
        info.minY = (float) down.Z;
        info.maxY = (float) top.Z;

        //save footprint as gps, ghost can be created
        info.footprint = new List<GpsCorner>();
        foreach (Vector3 p in currFootprint) {
            ArcGISPoint geo = generator.mapComponent.EngineToGeographic(p);
            GpsCorner corner = new GpsCorner();
            corner.lat = geo.Y;
            corner.lon = geo.X;
            info.footprint.Add(corner);
        }

        //create save struct
        MissionSave save = new MissionSave();
        save.mission = currentMission;
        save.building = info;

        //serialize and proceed to save
        string json = JsonConvert.SerializeObject(save);
        string folder = Path.Combine(Application.persistentDataPath, "Missions");
        Directory.CreateDirectory(folder);
        string filename = $"mission_{name}.json";
        string path = Path.Combine(folder, filename);
        File.WriteAllText(path, json);

        Toast.call.Show($"Mission saved!", 2f, false);
        return path;
    }

    public bool LoadMission(string path) {
        if (!File.Exists(path)) {
            Toast.call.Show($"Mission file not found!", 2f, true);
            return false;
        }

        //deserialize file
        MissionSave loaded = JsonConvert.DeserializeObject<MissionSave>(File.ReadAllText(path));

        if (loaded == null || loaded.mission.route == null || loaded.mission.route.segments == null || loaded.mission.route.segments.Count == 0) {
            Toast.call.Show($"Invalid mission file!", 2f, true);
            return false;
        }

        //load points
        List<Point> points = loaded.mission.route.segments[0].multipoint.points;
        if (points == null || points.Count < 3) {
            Toast.call.Show($"Mission file contains no points!", 2f, true);
            return false;
        }

        //update parameters from file to generator so regeneration is possible
        Parameters param = loaded.mission.route.segments[0].parameters;
        if (param != null) {
            generator.scanDistance = param.scanDistance;
            paramMaxHeight = param.maxHeight;
            paramMinHeight = param.minHeight;
            paramOverlap = param.overlapForward;
        }

        //load footprint
        List<Vector3> footprint = new List<Vector3>();
        if (loaded.building?.footprint != null) {
            foreach (GpsCorner cor in loaded.building.footprint) {
                ArcGISPoint geo = new ArcGISPoint(cor.lon, cor.lat, 0, new ArcGISSpatialReference(4326));
                footprint.Add(generator.mapComponent.GeographicToEngine(geo));
            }
        }

        double lat = loaded.building.footprint[0].lat;
        double lon = loaded.building.footprint[0].lon;
        float minY = generator.mapComponent.GeographicToEngine(new ArcGISPoint(lon, lat, loaded.building.minY, new ArcGISSpatialReference(4326))).y;
        float maxY = generator.mapComponent.GeographicToEngine(new ArcGISPoint(lon, lat, loaded.building.maxY, new ArcGISSpatialReference(4326))).y;

        //create ghost building
        Bounds bounds = new Bounds(footprint[0], Vector3.zero);
        foreach (Vector3 p in footprint) {
            bounds.Encapsulate(p);
        }
        GameObject ghost = new GameObject("LoadedMission_" + loaded.mission.route.name);
        ghost.transform.position = new Vector3(bounds.center.x, 0f, bounds.center.z);
        BoxCollider box = ghost.AddComponent<BoxCollider>();
        box.center = new Vector3(0, (minY + maxY) / 2f, 0);
        box.size = new Vector3(bounds.size.x, maxY - minY, bounds.size.z);

        //load path from gps to relative
        List<Vector3> flightpath = new List<Vector3>();
        for (int i = 1; i < points.Count - 1; i++) {
            ArcGISPoint geo = new ArcGISPoint(points[i].longitude, points[i].latitude, points[i].altitude, new ArcGISSpatialReference(4326));
            Vector3 pos = generator.mapComponent.GeographicToEngine(geo);
            flightpath.Add(pos);
        }

        //hand over operations to generator
        generator.ClearPath();
        generator.VisualizePath(flightpath);
        currentMission = loaded.mission;

        SetBuilding(ghost, footprint);

        //update UI about new mission, focus camera
        MissionUI.Instance?.SetNewMission(ghost, footprint, loaded.building.name);
        Camera.main.transform.position = new Vector3(bounds.center.x, maxY + 20f, bounds.center.z);

        Toast.call.Show($"Mission loaded!", 2f, false);
        return true;
    }

    public bool DeleteMission(string path) {
        if (!File.Exists(path)) {
            Toast.call.Show($"Mission file not found!", 2f, true);
            return false;
        }

        File.Delete(path);
        Toast.call.Show($"Mission deleted!", 2f, false);
        return true;
    }

    public void DeleteWaypoints(List<GameObject> waypoints) {
        if (waypoints == null || waypoints.Count == 0 || generator == null)
            return;

        //ordered
        List<GameObject> ordered = generator.GetMissionWaypoints();
        ordered.Sort((a, b) => MissionGenerator.GetWaypointIdx(a).CompareTo(MissionGenerator.GetWaypointIdx(b)));

        HashSet<GameObject> deleting = new HashSet<GameObject>(waypoints);
        List<GameObject> remaining = ordered.Where(wp => !deleting.Contains(wp)).ToList();

        /*
        //get all tubes connected
        HashSet<GameObject> deletingTubes = new HashSet<GameObject>();
        foreach (GameObject wp in waypoints) {
            if (wp == null || !generator.tubeMap.ContainsKey(wp))
                continue;
            foreach (GameObject tube in generator.tubeMap[wp])
                deletingTubes.Add(tube);
        }

        //remove deleted tubes from remaining WP tubeMap
        foreach (GameObject wp in remaining) {
            if (generator.tubeMap.ContainsKey(wp))
                generator.tubeMap[wp].RemoveAll(t => deletingTubes.Contains(t));
        }

        //destroy tubes
        foreach (GameObject tube in deletingTubes) {
            generator.spawnedObjects.Remove(tube);
            Destroy(tube);
        }
        */
        generator.RemoveTubes(waypoints, remaining);
        generator.RemoveWaypoint(waypoints);

        /*
        //destroy waypoints
        foreach (GameObject wp in waypoints) {
            if (wp == null)
                continue;
            generator.tubeMap.Remove(wp);
            generator.spawnedObjects.Remove(wp);
            Destroy(wp);
        }
        */

        ClipFootprint();

        //reconnect the path after deleting
        if (autoConnect) {
            Debug.LogWarning("Running auto connect");
            ConnectPath(ordered, deleting, remaining);
        } else {
            Debug.LogWarning("Running level reconnect");
            generator.ReconnectLevelPath(remaining);
            snakeMode = true;
        }

        List<int> idx = new List<int>();
        for (int i = 0; i < ordered.Count; i++) {
            if (deleting.Contains(ordered[i]))
                idx.Add(i);
        }
        generator.RemoveNormalsIdx(idx);

        /*
        //refresh normals
        if (generator.helixNormals != null) {
            List<int> deleteIndices = ordered
                .Select((wp, idx) => new { wp, idx })
                .Where(x => deleting.Contains(x.wp))
                .Select(x => x.idx)
                .OrderByDescending(i => i)
                .ToList();
            foreach (int idx in deleteIndices)
                if (idx < generator.helixNormals.Count)
                    generator.helixNormals.RemoveAt(idx);
        }
        */
        MissionUI.Instance?.RefreshListUI();
    }

    private void ConnectPath(List<GameObject> ordered, HashSet<GameObject> deleting, List<GameObject> remaining) {
        if (generator.use3DTubes) {
            for (int i = 0; i < remaining.Count - 1; i++) {
                int idxA = ordered.IndexOf(remaining[i]);
                int idxB = ordered.IndexOf(remaining[i + 1]);

                if (idxB - idxA > 1) {
                    Vector3 posA = remaining[i].transform.position;
                    Vector3 posB = remaining[i + 1].transform.position;

                    //calc pushdir for solving collisions
                    Vector3 mid = (posA + posB) / 2f;
                    Vector3 pushDir = (mid - missionCenter).normalized;
                    pushDir.y = 0f;

                    //used in generating path
                    List<Vector3> bridgePoints = generator.SolveSightline(posA, posB, pushDir);

                    //create tubes in disconnected
                    Vector3 prev = posA;
                    foreach (Vector3 bp in bridgePoints) {
                        generator.AddTube(remaining[i], remaining[i + 1], prev, bp);
                        prev = bp;
                    }
                }
            }
        } else {
            generator.UpdateLineRenderer(remaining.Select(wp => wp.transform.position).ToList());
        }
    }

    private void ClipFootprint() {
        //get remaining waypoints sorted by WP index
        List<GameObject> remaining = generator.GetMissionWaypoints();
        if (remaining == null || remaining.Count < 2)
            return;

        //sort wps by index
        remaining.Sort((a, b) => MissionGenerator.GetWaypointIdx(a).CompareTo(MissionGenerator.GetWaypointIdx(b)));

        //calculate center of remaining wps for clip
        Vector3 remainingCenter = Vector3.zero;
        foreach (var wp in remaining)
            remainingCenter += new Vector3(wp.transform.position.x, 0, wp.transform.position.z);
        remainingCenter /= remaining.Count;
        List<Vector3> clipped = new List<Vector3>(currFootprint);

        for (int i = 0; i < remaining.Count - 1; i++) {
            int idxA = MissionGenerator.GetWaypointIdx(remaining[i]);
            int idxB = MissionGenerator.GetWaypointIdx(remaining[i + 1]);

            //creating shortcuts
            if (idxB - idxA <= 1)
                continue;

            //clip footprint by line between wps
            Vector3 A = remaining[i].transform.position;
            Vector3 B = remaining[i + 1].transform.position;
            A.y = 0;
            B.y = 0;

            //clip over disconnected segment
            List<Vector3> res = ClipByLine(clipped, A, B, remainingCenter);
            if (res.Count >= 3)
                clipped = res;
            else
                Debug.LogWarning("clipping skipped");
        }

        if (clipped.Count >= 3) {
            currFootprint = clipped;
            Debug.Log("footprint updated");
        }
    }

    private List<Vector3> ClipByLine(List<Vector3> polygon, Vector3 A, Vector3 B, Vector3 keepSide) {
        Vector3 lineDir = (B - A).normalized;
        // Perpendicular in XZ plane
        Vector3 normal = new Vector3(-lineDir.z, 0, lineDir.x);
        // Orient toward centroid
        if (Vector3.Dot(keepSide - A, normal) < 0)
            normal = -normal;

        List<Vector3> result = new List<Vector3>();
        int n = polygon.Count;

        for (int i = 0; i < n; i++) {
            Vector3 curr = polygon[i];
            Vector3 next = polygon[(i + 1) % n];

            // Signed distances to the clip line (XZ only)
            float d1 = Vector3.Dot(new Vector3(curr.x, 0, curr.z) - A, normal);
            float d2 = Vector3.Dot(new Vector3(next.x, 0, next.z) - A, normal);

            if (d1 >= 0)
                result.Add(curr); // inside, keep (with original Y)

            // Edge crosses the boundary -> add intersection point
            if ((d1 < 0 && d2 > 0) || (d1 > 0 && d2 < 0)) {
                float t = d1 / (d1 - d2);
                result.Add(Vector3.Lerp(curr, next, t));
            }
        }

        return result;
    }

    public List<string> GetAllMissions() {
        //list all json files in Missions folder
        string folder = Path.Combine(Application.persistentDataPath, "Missions");
        Directory.CreateDirectory(folder);
        string[] files = Directory.GetFiles(folder, "*.json");
        List<string> missions = new List<string>(files);
        return missions;
    }

    public GameObject GetCurrentBuilding() {
        return currBuilding;
    }

    public Vector3 GetNormal(int idx) {
        return generator.GetNormal(idx);
    }

    public void SetScanDistance(float value) {
        generator.scanDistance = value;
        Regenerate();
    }

    public void SetVerticalStep(float value) {
        generator.verticalStep = value;
        Regenerate();
    }

    public void SetSegmentLen(float value) {
        generator.maxSegmentLen = value;
        Regenerate();
    }

    public void SetWaypointSize(float value) {
        generator.waypointSize = value;
        Regenerate();
    }

    public void SetFlightSpeed(float value) {
        generator.flightSpeed = value;
    }

    public void SetUse3DTubes(bool value) {
        generator.use3DTubes = value;
        Regenerate();
    }

    public void SetPathColor(Color value) {
        generator.pathColor = value;
        Regenerate();
    }

    public void SetWidthOverlap(float value) {
        generator.widthOverlap = value;
        generator.RecalculateSteps();
        Regenerate();
    }

    public void SetHeightOverlap(float value) {
        generator.heightOverlap = value;
        generator.RecalculateSteps();
        Regenerate();
    }

    public void SetBuilding(GameObject building, List<Vector3> footprint) {
        currBuilding = building;
        currFootprint = new List<Vector3>(footprint);
        snakeMode = false;
    }

    public void SetMissionType(int index) {
        snakeMode = false;
        if (index == 0) {
            generator.missionType = MissionType.Horizontal;
        } else if (index == 1) {
            generator.missionType = MissionType.Vertical;
        }
        Regenerate();
    }

    public void SetSnakeMode(bool value) {
        snakeMode = value;
    }

    public void ClearBuilding() {
        currBuilding = null;
        currFootprint = null;
        snakeMode = false;
    }

    public bool IsMissionRunning() {
        if (!settings.isWaypointMission) {
            return navigator.IsMissionRunning();
        } else {
            //TODO implement for waypoint mission
            return false;
        }
    }
}
