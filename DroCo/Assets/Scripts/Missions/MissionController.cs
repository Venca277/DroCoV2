using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
public class NetworkWrapper {
    public string type;
    public MissionData data;
}

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


    void Start() {

    }

    // Update is called once per frame
    void Update() {
        if (droneManager == null)
            return;

        Drone drone = droneManager.GetFirstDrone();
        if (drone == null)
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
}
