using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System.Windows.Input;
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
    public List<Vector3> curentMissionNormals;
    public List<Vector3> currentMissionSticks;
    public Vector3 missionCenter;
    private GameObject currBuilding;
    private List<Vector3> currFootprint;

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

    public bool Regenerate() {
        if (currBuilding == null || currFootprint == null)
            return false;

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
        //TODO dronemissioncontroller is deprecated might delete later
        if (currentMission != null) {
            UpdateMissionFromWaypoints();
            if (settings.isWaypointMission) {
                SendMissionToNetwork(currentMission);
            } else {
                List<Vector3> normals = generator.GetMissionNormals(currentMissionSticks, generator.GetMissionCenter(currentMissionSticks));
                navigator.StartMission("", currentMissionSticks, normals);
            }
        } else {
            Debug.LogError("No mission ready to start. Try selecting a building first.");
        }
    }

    public void MissionStop() {
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

        UpdateMissionFromWaypoints();
        currentMission.route.name = name;

        Collider col = currBuilding.GetComponent<Collider>();
        BuildingInfo info = new BuildingInfo();
        info.name = name;

        //preserving global height which could be different in different map centering
        Bounds b = col.bounds;
        ArcGISPoint down = generator.mapComponent.EngineToGeographic(new Vector3(b.center.x, b.min.y, b.center.z));
        ArcGISPoint top = generator.mapComponent.EngineToGeographic(new Vector3(b.center.x, b.max.y, b.center.z));
        info.minY = (float) down.Z;
        info.maxY = (float) top.Z;

        info.footprint = new List<GpsCorner>();
        foreach (Vector3 p in currFootprint) {
            ArcGISPoint geo = generator.mapComponent.EngineToGeographic(p);
            GpsCorner corner = new GpsCorner();
            corner.lat = geo.Y;
            corner.lon = geo.X;
            info.footprint.Add(corner);
        }

        MissionSave save = new MissionSave();
        save.mission = currentMission;
        save.building = info;

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

        MissionSave loaded = JsonConvert.DeserializeObject<MissionSave>(File.ReadAllText(path));

        if (loaded == null || loaded.mission.route == null || loaded.mission.route.segments == null || loaded.mission.route.segments.Count == 0) {
            Toast.call.Show($"Invalid mission file!", 2f, true);
            return false;
        }

        List<Point> points = loaded.mission.route.segments[0].multipoint.points;
        if (points == null || points.Count < 3) {
            Toast.call.Show($"Mission file contains no points!", 2f, true);
            return false;
        }

        Parameters param = loaded.mission.route.segments[0].parameters;
        if (param != null) {
            generator.scanDistance = param.scanDistance;
            paramMaxHeight = param.maxHeight;
            paramMinHeight = param.minHeight;
            paramOverlap = param.overlapForward;
        }

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

        Bounds bounds = new Bounds(footprint[0], Vector3.zero);
        foreach (Vector3 p in footprint) {
            bounds.Encapsulate(p);
        }
        GameObject ghost = new GameObject("LoadedMission_" + loaded.mission.route.name);
        ghost.transform.position = new Vector3(bounds.center.x, 0f, bounds.center.z);
        BoxCollider box = ghost.AddComponent<BoxCollider>();
        box.center = new Vector3(0, (minY + maxY) / 2f, 0);
        box.size = new Vector3(bounds.size.x, maxY - minY, bounds.size.z);

        List<Vector3> flightpath = new List<Vector3>();
        for (int i = 1; i < points.Count - 1; i++) {
            ArcGISPoint geo = new ArcGISPoint(points[i].longitude, points[i].latitude, points[i].altitude, new ArcGISSpatialReference(4326));
            Vector3 pos = generator.mapComponent.GeographicToEngine(geo);
            flightpath.Add(pos);
        }

        generator.ClearPath();
        generator.VisualizePath(flightpath);
        currentMission = loaded.mission;

        SetBuilding(ghost, footprint);

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
        ordered.Sort((a, b) => {
            int ia = int.Parse(a.name.Replace("WP_", ""));
            int ib = int.Parse(b.name.Replace("WP_", ""));
            return ia.CompareTo(ib);
        });

        HashSet<GameObject> deleting = new HashSet<GameObject>(waypoints);
        List<GameObject> remaining = ordered.Where(wp => !deleting.Contains(wp)).ToList();

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

        //destroy waypoints
        foreach (GameObject wp in waypoints) {
            if (wp == null)
                continue;
            generator.tubeMap.Remove(wp);
            generator.spawnedObjects.Remove(wp);
            Destroy(wp);
        }

        //reconnect the path after deleting
        ConnectPath(ordered, deleting, remaining);

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
                        //temp point
                        generator.AddTube(remaining[i], remaining[i + 1]);
                        prev = bp;
                    }
                }
            }
        } else {
            generator.UpdateLineRenderer(remaining.Select(wp => wp.transform.position).ToList());
        }
    }

    public List<string> GetAllMissions() {
        string folder = Path.Combine(Application.persistentDataPath, "Missions");
        Directory.CreateDirectory(folder);
        string[] files = Directory.GetFiles(folder, "*.json");
        List<string> missions = new List<string>(files);
        return missions;
    }

    public GameObject GetCurrentBuilding() {
        return currBuilding;
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
        // pozor: flightSpeed neregeneruje cestu, jen mění rychlost
    }

    public void SetUse3DTubes(bool value) {
        generator.use3DTubes = value;
        Regenerate();
    }

    public void SetPathColor(Color value) {
        generator.pathColor = value;
        Regenerate();
    }

    public void SetBuilding(GameObject building, List<Vector3> footprint) {
        currBuilding = building;
        currFootprint = footprint;
    }

    public void ClearBuilding() {
        currBuilding = null;
        currFootprint = null;
    }

    public bool IsMissionRunning() {
        if (!settings.isWaypointMission) {
            return navigator.isMissionRunning;
        } else {
            //TODO implement for waypoint mission
            return false;
        }
    }
}
