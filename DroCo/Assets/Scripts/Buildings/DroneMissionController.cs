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

    public void ProcessMission(GameObject building, List<Vector3> footprint) {
        Debug.Log("--- ZAČÍNÁM PLÁNOVAT MISI ---");

        // 1. Vygenerovat spirálu (Unity metry)
        List<Vector3> rawHelixUnity = generator.GenerateScanPath(building, footprint);

        if (rawHelixUnity == null || rawHelixUnity.Count == 0) {
            Debug.LogError("Chyba: Generátor nevrátil žádnou trasu!");
            return;
        }

        // 2. Převést na GPS (Lat/Lon/Alt) - vrací nám to pomocnou třídu z minula,
        // kterou teď převedeme do VAŠÍ struktury Point.
        List<GPSWaypoint> gpsHelix = generator.ConvertToGPSCoordinates(rawHelixUnity);

        // 3. Naplnění VAŠÍ existující struktury MissionData
        MissionData complexMission = new MissionData();
        complexMission.route = new Route();
        complexMission.route.name = "Generated Helix Scan";
        complexMission.route.segments = new List<Segment>();

        // Vytvoříme jeden segment pro celou spirálu
        Segment scanSegment = new Segment();
        scanSegment.type = "scan"; // Nebo jakýkoliv typ používáte

        // Nastavení parametrů
        scanSegment.parameters = new Parameters();
        scanSegment.parameters.maxHeight = paramMaxHeight;
        scanSegment.parameters.minHeight = paramMinHeight;
        scanSegment.parameters.overlapForward = paramOverlap;
        scanSegment.parameters.overlapSide = paramOverlap;
        scanSegment.parameters.scanDistance = generator.scanDistance;
        scanSegment.parameters.scanPattern = "Helix";

        // Naplnění bodů
        scanSegment.multipoint = new MultiPoint();
        scanSegment.multipoint.points = new List<Point>();

        // A) Přidat Start
        AddPointToSegment(scanSegment, startLat, startLon, startAlt);

        // B) Přidat Spirálu
        foreach (var wp in gpsHelix) {
            AddPointToSegment(scanSegment, wp.latitude, wp.longitude, wp.altitude);
        }

        // C) Přidat Návrat
        AddPointToSegment(scanSegment, startLat, startLon, startAlt);

        // Přidáme segment do trasy
        complexMission.route.segments.Add(scanSegment);

        // 4. Odeslání
        SendMissionToNetwork(complexMission);
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
