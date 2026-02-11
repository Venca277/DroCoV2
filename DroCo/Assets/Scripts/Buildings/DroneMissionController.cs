using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json; // Doporučeno, nebo použijte JsonUtility

// Pomocná obálka pouze pro síťovou komunikaci (aby Python poznal typ zprávy)
[System.Serializable]
public class NetworkWrapper {
    public string type;       // např. "mission_upload"
    public MissionData data;  // ZDE POUŽIJEME VAŠI EXISTUJÍCÍ TŘÍDU
}

public class DroneMissionController : MonoBehaviour {

    [Header("Reference")]
    public MissionGenerator generator;

    [Header("Startovní Pozice (Home)")]
    public double startLat = 49.226015;
    public double startLon = 16.597071;
    public double startAlt = 250.0;

    // Parametry pro skenování (vyplníme do vaší struktury)
    [Header("Parametry Skenu")]
    public float paramMaxHeight = 50f;
    public float paramMinHeight = 10f;
    public float paramOverlap = 0.5f;

    public MissionData currentMission;

    public void ProcessMission(GameObject building, List<Vector3> footprint) {
        Debug.Log("--- MISSION PLANNING ---");

        //generate path
        List<Vector3> rawHelixUnity = generator.GenerateScanPath(building, footprint);

        if (rawHelixUnity == null || rawHelixUnity.Count == 0) {
            Debug.LogError("Error: Generator returned no path!");
            return;
        }

        //converting to gps coords
        //put in point structure
        List<GPSWaypoint> gpsHelix = generator.ConvertToGPSCoordinates(rawHelixUnity);

        //put in complex structure
        MissionData complexMission = new MissionData();
        complexMission.route = new Route();
        complexMission.route.name = "Generated Helix Scan";
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
            AddPointToSegment(scanSegment, wp.latitude, wp.longitude, wp.altitude);
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
        GameObject[] allobjs = FindObjectsOfType<GameObject>();
        List<GameObject> allwps = new List<GameObject>();

        foreach (GameObject obj in allobjs) {
            if (obj.name.StartsWith("WP_")) {
                allwps.Add(obj);
            }
        }

        //sort with names
        allwps.Sort((a, b) => {
            int numA = int.Parse(a.name.Replace("WP_", ""));
            int numB = int.Parse(b.name.Replace("WP_", ""));
            return numA.CompareTo(numB);
        });

        //list of positions
        List<Vector3> updatedPath = new List<Vector3>();
        foreach (GameObject wp in allwps) {
            updatedPath.Add(wp.transform.position);
        }

        //gps conversion
        List<GPSWaypoint> updatedGPS = generator.ConvertToGPSCoordinates(updatedPath);

        //back to currmission
        currentMission.route.segments[0].multipoint.points.Clear();

        AddPointToSegment(currentMission.route.segments[0], startLat, startLon, startAlt);

        foreach (GPSWaypoint gps in updatedGPS) {
            AddPointToSegment(currentMission.route.segments[0], gps.latitude, gps.longitude, gps.altitude);
        }

        AddPointToSegment(currentMission.route.segments[0], startLat, startLon, startAlt);
    }

    public void MissionStart() {
        if (currentMission != null) {
            UpdateMissionFromWaypoints();
            SendMissionToNetwork(currentMission);
        } else {
            Debug.LogError("No mission ready to start. Try selecting a building first.");
        }
    }

    // Pomocná funkce pro převod do vaší třídy Point
    private void AddPointToSegment(Segment segment, double lat, double lon, double alt) {
        Point p = new Point();
        p.latitude = lat;
        p.longitude = lon;
        p.altitude = alt;
        p.altitudeType = "AMSL"; // Nebo "Relative", podle toho co používáte
        segment.multipoint.points.Add(p);
    }

    private void SendMissionToNetwork(MissionData dataStructure) {
        if (WebSocketServer.Instance == null) {
            Debug.LogError("WebSocketServer neběží! Nemohu odeslat misi.");
            return;
        }

        // Zabalíme vaši strukturu do obálky s typem zprávy
        NetworkWrapper msg = new NetworkWrapper();
        msg.type = "mission_upload";
        msg.data = dataStructure;

        // Serializace (Newtonsoft je lepší pro složité vnořené třídy)
        string json = JsonConvert.SerializeObject(msg);
        // Pokud nemáte Newtonsoft, použijte: string json = JsonUtility.ToJson(msg);

        // Odeslání pomocí nové metody, kterou jsme přidali do Serveru
        WebSocketServer.Instance.BroadcastToAll(json);

        Debug.Log(">>> MISE ODESLÁNA (Complex Structure) <<<");
        // Debug.Log(json); // Odkomentujte pro kontrolu JSONu
    }
}
