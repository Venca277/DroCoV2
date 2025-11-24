using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Geometry;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class MissionManager : Singleton<MissionManager> {

    private enum PointDirection {
        DOWN,
        UP
    }

    private static float MAVIC_HFOV = 35.3f;

    private List<DistanceBillboard> distancesList = new List<DistanceBillboard>();

    [SerializeField]
    private Transform ExperimentScene;
    [SerializeField]
    private GameObject ActionPointPrefab;
    [SerializeField]
    private Material ConnectionMaterial;
    [SerializeField]
    private GameObject WaypointPrefab;

    private GameObject APInstance;
    private Collider apCollider;

    private List<Waypoint> missionWaypoints = new List<Waypoint>();
    //private List<MissionSegment> missionSegments = new List<MissionSegment>();


    private void Start() {
        
    }

    private void Update() {

    }

    public void AddToDistanceList(DistanceBillboard billboard) {
        distancesList.Add(billboard);
    }

    public void ChangeTargetFaceCamera(Transform target) {
        foreach (DistanceBillboard billboard in distancesList) {
            billboard.ObjectToFace = target;
        }
    }

    public void ChangeTarget(Transform target) {
        foreach (DistanceBillboard billboard in distancesList) {
            billboard.ObjectToComputeDistance = target;
        }
    }

    public void LoadMission() {
        //StartCoroutine(DownloadMission());
        StartCoroutine(DownloadMissionJson());
    }

    public IEnumerator DownloadMissionJson(string url = "https://nextcloud.fit.vutbr.cz/s/R298KkbkCFPWGJF/download/AR_test.json") {
    //public IEnumerator DownloadMissionJson(string url = "https://nextcloud.fit.vutbr.cz/s/jqbaR6aC54Hp5sY/download/AR_test_short.json") {
        UnityWebRequest www;

        Debug.Log("DOWNLOADING MISSION");

        try {
            www = UnityWebRequest.Get(url);
        } catch (WebException ex) {
            Debug.LogException(ex);
            yield break;
        }

        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError) {
            Debug.LogError(www.error + " (" + url + ")");
        } else {
            OnMissionDownloadedJson(www.downloadHandler.text);
        }
    }

    private void OnMissionDownloadedJson(string json) {
        // Destroy previous mission
        DestroyMission();

        Debug.Log(json);

        // Deserialize the JSON
        MissionData missionData = JsonConvert.DeserializeObject<MissionData>(json);
        List<MissionSegment> segments = ExtractMissionSegments(missionData);

        // Spawn mission waypoints on the map
        GenerateMissionWaypoints(segments);
    }

    private List<MissionSegment> ExtractMissionSegments(MissionData missionData) {
        List<Waypoint> segmentPoints = new List<Waypoint>();
        List<MissionSegment> missionSegments = new List<MissionSegment>();

        // Extract waypoints from the mission data
        foreach (Segment segment in missionData.route.segments) {
            foreach (Point point in segment.multipoint.points) {
                // Convert latitude and longitude from radians to degrees
                double latitude = point.latitude * Mathf.Rad2Deg;
                double longitude = point.longitude * Mathf.Rad2Deg;

                // Add the waypoint to the mission waypoints list
                segmentPoints.Add(new Waypoint(new GPS() { longitude = longitude, latitude = latitude }, point.altitude));
            }

            MissionSegment missionSegment = new MissionSegment();

            // Separate waypoints by lines
            for (int i = 0; i + 1 < segmentPoints.Count; i++) {
                missionSegment.Lines.Add(new WaypointLine(segmentPoints[i], segmentPoints[i + 1]));
            }

            missionSegment.SegmentParameters = segment.parameters;
            missionSegments.Add(missionSegment);

            segmentPoints.Clear();
        }

        return missionSegments;
    }

    private double CalculateDistance(ArcGISPoint point1, ArcGISPoint point2) {
        // Calculate the geodetic distance between two points
        ArcGISGeodeticDistanceResult geodeticCurve = ArcGISGeometryEngine.DistanceGeodetic(point1, point2, new ArcGISLinearUnit(ArcGISLinearUnitId.Meters), new ArcGISAngularUnit(ArcGISAngularUnitId.Degrees), ArcGISGeodeticCurveType.Geodesic);

        // Return the distance in meters
        return geodeticCurve.Distance;
    }

    private float CalculateHorizontalStep(Parameters parameters) {
        float horizontalCoverage = 2 * parameters.scanDistance * Mathf.Tan(Mathf.Deg2Rad * MAVIC_HFOV);
        return horizontalCoverage * (1 - parameters.overlapSide / 100);
    }

    private double CalculateStartingOffset(double distance, ref int numberOfSteps, float stepSize) {
        double startingOffset = (distance - (numberOfSteps * stepSize)) / 2;
        // Adjust the offset to be atleast 0.8 meters
        if (startingOffset < 0.8) {
            numberOfSteps -= 1;
            startingOffset = (distance - (numberOfSteps * stepSize)) / 2;
        }

        return startingOffset;
    }

    private void GenerateMissionWaypoints(List<MissionSegment> segments) {
        PointDirection firstPoint = PointDirection.DOWN;
        List<WaypointGameObject> waypoints = new List<WaypointGameObject>();

        foreach (MissionSegment segment in segments) {
            // Compute coverage
            float horizontalStep = CalculateHorizontalStep(segment.SegmentParameters);
            int stepSize = Mathf.RoundToInt(horizontalStep);

            foreach (WaypointLine line in segment.Lines) {
                ArcGISPoint pointA = new ArcGISPoint(line.WaypointA.Coordinates.longitude, line.WaypointA.Coordinates.latitude, line.WaypointA.Altitude, new ArcGISSpatialReference(4326));
                ArcGISPoint pointB = new ArcGISPoint(line.WaypointB.Coordinates.longitude, line.WaypointB.Coordinates.latitude, line.WaypointB.Altitude, new ArcGISSpatialReference(4326));
                double distance = CalculateDistance(pointA, pointB);
                int numberOfSteps = (int) distance / stepSize;                

                double startingOffset = CalculateStartingOffset(distance, ref numberOfSteps, stepSize);

                GenerateWaypointsAlongLine(pointA, pointB, startingOffset, numberOfSteps, distance, stepSize, segment.SegmentParameters, ref firstPoint, waypoints);

                if (segment.Lines.Count > 1 && segment.Lines.IndexOf(line) != segment.Lines.Count - 1) {
                    if (firstPoint == PointDirection.DOWN) {
                        waypoints.Add(CreateWaypoint(pointB, segment.SegmentParameters.minHeight));
                    } else {
                        waypoints.Add(CreateWaypoint(pointB, segment.SegmentParameters.maxHeight));
                    }
                }
            }
        }

        ConnectWaypoints(waypoints);
    }


    private void GenerateWaypointsAlongLine(ArcGISPoint pointA, ArcGISPoint pointB, double startingOffset, int numberOfSteps, double distance, int stepSize, Parameters parameters, ref PointDirection firstPoint, List<WaypointGameObject> waypoints) {
        
        for (int i = 0; i <= numberOfSteps; i++) {
            double fraction = (startingOffset + i * stepSize) / distance;
            double latitude = Mathf.Lerp((float) pointA.Y, (float) pointB.Y, (float) fraction);
            double longitude = Mathf.Lerp((float) pointA.X, (float) pointB.X, (float) fraction);
            double altitude = Mathf.Lerp((float) pointA.Z, (float) pointB.Z, (float) fraction);

            CreateWaypointPair(latitude, longitude, altitude, pointA.SpatialReference, parameters, ref firstPoint, waypoints);
        }
    }

    private void CreateWaypointPair(double latitude, double longitude, double altitude, ArcGISSpatialReference spatialReference, Parameters parameters, ref PointDirection firstPoint, List<WaypointGameObject> waypoints) {
        if (firstPoint == PointDirection.DOWN) {
            waypoints.Add(CreateWaypoint(latitude, longitude, altitude, spatialReference, parameters.minHeight));
            waypoints.Add(CreateWaypoint(latitude, longitude, altitude, spatialReference, parameters.maxHeight));
            firstPoint = PointDirection.UP;
        } else {
            waypoints.Add(CreateWaypoint(latitude, longitude, altitude, spatialReference, parameters.maxHeight));
            waypoints.Add(CreateWaypoint(latitude, longitude, altitude, spatialReference, parameters.minHeight));
            firstPoint = PointDirection.DOWN;
        }
    }

    private WaypointGameObject CreateWaypoint(double latitude, double longitude, double altitude, ArcGISSpatialReference spatialReference, float offset) {
        GameObject waypoint = Instantiate(WaypointPrefab, GameManager.Instance.Scene3DView.transform);
        ArcGISLocationComponent locationComponent = waypoint.AddComponent<ArcGISLocationComponent>();

        locationComponent.Position = new ArcGISPoint(longitude, latitude, altitude, spatialReference);
        locationComponent.SurfacePlacementMode = ArcGISSurfacePlacementMode.RelativeToGround;
        locationComponent.SurfacePlacementOffset = offset;

        Waypoint wp = new Waypoint(new GPS() { latitude = latitude, longitude = longitude }, altitude: locationComponent.Position.Z);
        wp.SetVisual(waypoint);
        missionWaypoints.Add(wp);

        return waypoint.GetComponent<WaypointGameObject>();
    }

    private WaypointGameObject CreateWaypoint(ArcGISPoint point, float offset) {
        GameObject waypoint = Instantiate(WaypointPrefab, GameManager.Instance.Scene3DView.transform);
        ArcGISLocationComponent locationComponent = waypoint.AddComponent<ArcGISLocationComponent>();

        locationComponent.Position = point;
        locationComponent.SurfacePlacementMode = ArcGISSurfacePlacementMode.RelativeToGround;
        locationComponent.SurfacePlacementOffset = offset;

        Waypoint wp = new Waypoint(new GPS() { latitude = point.X, longitude = point.Y }, altitude: point.Z);
        wp.SetVisual(waypoint);
        missionWaypoints.Add(wp);

        return waypoint.GetComponent<WaypointGameObject>();
    }

    private void ConnectWaypoints(List<WaypointGameObject> waypoints) {
        WaypointGameObject previousWaypoint = null;
        int i = 0;
        foreach (WaypointGameObject waypoint in waypoints) {
            waypoint.SetText(i.ToString());
            i++;

            // Make connection between waypoints
            if (previousWaypoint != null) {
                ConnectionManager.CreateConnection(previousWaypoint.ConnectionBinder, waypoint.ConnectionBinder);
            }

            previousWaypoint = waypoint;
        }
    }

    //public IEnumerator DownloadMission(string url = "https://nextcloud.fit.vutbr.cz/s/wA7FzncKQJWRBGw/download/AR_test.kml") {
    public IEnumerator DownloadMission(string url = "https://nextcloud.fit.vutbr.cz/s/MtNdCAycjqtwxPY/download/AR_test_short.kml") {
        UnityWebRequest www;

        Debug.Log("DOWNLOADING MISSION");

        try {
            www = UnityWebRequest.Get(url);
        } catch (WebException ex) {
            Debug.LogException(ex);
            yield break;
        }

        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError) {
            Debug.LogError(www.error + " (" + url + ")");
        } else {
            OnMissionDownloaded(www.downloadHandler.text);
        }
    }

    private void OnMissionDownloaded(string kml) {
        //Destroy previous mission
        DestroyMission();

        string startKeyword = "<coordinates>";
        string endKeyword = "</coordinates>";
        int start = kml.IndexOf(startKeyword);
        int end = kml.IndexOf(endKeyword);
        string missionStr = kml.Substring(start + startKeyword.Length, end - start - startKeyword.Length);

        string[] points = missionStr.Split(" ");

        foreach (string point in points) {
            string[] coords = point.Split(",");
            missionWaypoints.Add(new Waypoint(new GPS() { longitude = double.Parse(coords[0]), latitude = double.Parse(coords[1]) }, double.Parse(coords[2])));
        }

        SpawnMission();
    }

    private void SpawnMission() {
        WaypointGameObject previousWaypoint = null;
        int i = 0;
        foreach(Waypoint waypoint in missionWaypoints) {
            GameObject point = Instantiate(WaypointPrefab, GameManager.Instance.Scene3DView.transform);
            ArcGISLocationComponent pointLocation = point.AddComponent<ArcGISLocationComponent>();
            pointLocation.Position = new ArcGISPoint(waypoint.Coordinates.longitude, waypoint.Coordinates.latitude, waypoint.Altitude, new ArcGISSpatialReference(4326));
            pointLocation.SurfacePlacementMode = ArcGISSurfacePlacementMode.RelativeToGround;
            pointLocation.SurfacePlacementOffset = 1f;
            WaypointGameObject waypointGO = point.GetComponent<WaypointGameObject>();
            waypointGO.SetText(i.ToString());
            i++;

            // Make connection between waypoints
            if (previousWaypoint != null) {
                ConnectionManager.CreateConnection(previousWaypoint.ConnectionBinder, waypointGO.ConnectionBinder);
            }

            previousWaypoint = waypointGO;

            waypoint.SetVisual(point);
        }
    }

    private void DestroyMission() {
        foreach (Waypoint waypoint in missionWaypoints) {
            waypoint.DestroyVisual();
        }
        missionWaypoints.Clear();

        ConnectionManager.CleanConnections();
        foreach (Transform child in transform) {
            Destroy(child.gameObject);
        }

    }

}

public class MissionSegment {
    public List<WaypointLine> Lines = new List<WaypointLine>();
    public Parameters SegmentParameters;

}

public class WaypointLine {
    public Waypoint WaypointA;
    public Waypoint WaypointB;

    public WaypointLine(Waypoint a, Waypoint b) {
        WaypointA = a;
        WaypointB = b;
    }
}


// Classes to match the JSON structure
[Serializable]
public class MissionData {
    public Route route;
}

[Serializable]
public class Route {
    public string name;
    public List<Segment> segments;
}

[Serializable]
public class Segment {
    public string type;
    public MultiPoint multipoint;
    public Parameters parameters;
}

[Serializable]
public class MultiPoint {
    public List<Point> points;
}

[Serializable]
public class Parameters {
    public float maxHeight;
    public float minHeight;
    public float overlapForward;
    public float overlapSide;
    public float scanDistance;
    public string scanPattern;
}

[Serializable]
public class Point {
    public double latitude;
    public double longitude;
    public double altitude;
    public string altitudeType;
}
