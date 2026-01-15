using System.Collections.Generic;
using UnityEngine;
using Esri.ArcGISMapsSDK.Components; // Nutné pro práci s mapou
using Esri.GameEngine.Geometry;       // Nutné pro ArcGISPoint

// Pomocná třída pro GPS souřadnice, kterou budeme posílat dronu
[System.Serializable]
public class GPSWaypoint {
    public double latitude;
    public double longitude;
    public double altitude;
}

public class MissionGenerator : MonoBehaviour {

    [Header("ArcGIS Reference")]
    public ArcGISMapComponent mapComponent; // SEM PŘETÁHNĚTE OBJEKT "Map" Z HIERARCHY!

    [Header("Parametry letu")]
    [Tooltip("Vzdálenost od stěny budovy (metry)")]
    public float scanDistance = 2.0f;

    [Tooltip("Výškový rozestup mezi závity (metry)")]
    public float verticalStep = 1.5f;

    [Header("Vzhled Trubky")]
    public bool use3DTubes = true;
    public Color pathColor = Color.blue;
    public float tubeThickness = 0.2f;

    [Header("Waypointy")]
    public GameObject waypointPrefab;
    public float waypointSize = 0.3f;

    // Interní
    private LineRenderer lineRenderer;
    private List<GameObject> spawnedObjects = new List<GameObject>();

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

    /// <summary>
    /// Hlavní funkce: Vygeneruje trasu, vykreslí ji v Unity a vrátí body ve World Space
    /// </summary>
    public List<Vector3> GenerateScanPath(GameObject buildingObj, List<Vector3> footprintPoints) {
        ClearPath();

        if (buildingObj == null || footprintPoints == null || footprintPoints.Count < 3)
            return new List<Vector3>();

        Bounds bounds = buildingObj.GetComponent<Collider>().bounds;
        float startY = bounds.min.y + 2.0f;
        float endY = bounds.max.y + 1.0f;

        // 1. Orbit Ring
        List<Vector3> orbitRing = new List<Vector3>();
        Vector3 centroid = Vector3.zero;
        foreach (var p in footprintPoints)
            centroid += p;
        centroid /= footprintPoints.Count;
        centroid.y = 0;

        foreach (var p in footprintPoints) {
            Vector3 pointFlat = new Vector3(p.x, 0, p.z);
            Vector3 dir = (pointFlat - centroid).normalized;
            Vector3 offsetPoint = pointFlat + (dir * scanDistance);
            orbitRing.Add(offsetPoint);
        }

        // 2. Helix Generace
        List<Vector3> finalPath = new List<Vector3>();
        float currentY = startY;

        while (currentY < endY) {
            for (int i = 0; i < orbitRing.Count; i++) {
                float progress = (float) i / orbitRing.Count;
                float heightOffset = verticalStep * progress;
                float actualY = currentY + heightOffset;

                if (actualY > endY)
                    break;

                Vector3 pt = orbitRing[i];
                Vector3 waypointPos = new Vector3(pt.x, actualY, pt.z);
                finalPath.Add(waypointPos);
            }
            currentY += verticalStep;
        }

        // 3. Vykreslení
        VisualizePath(finalPath);

        // Vrátíme Unity souřadnice, aby je Controller mohl převést
        return finalPath;
    }

    /// <summary>
    /// Převede Unity Vector3 body na reálné GPS (Lat/Lon/Alt)
    /// </summary>
    public List<GPSWaypoint> ConvertToGPSCoordinates(List<Vector3> unityPath) {
        List<GPSWaypoint> gpsPath = new List<GPSWaypoint>();

        if (mapComponent == null) {
            Debug.LogError("MissionGenerator: Není přiřazena ArcGIS Map Component! Nelze převádět souřadnice.");
            return gpsPath;
        }

        foreach (var point in unityPath) {
            // SDK funkce pro převod: Engine (Unity) -> Geographic (GPS)
            ArcGISPoint geoPos = mapComponent.EngineToGeographic(point);

            GPSWaypoint wp = new GPSWaypoint();
            wp.latitude = geoPos.Y;  // Y je Latitude
            wp.longitude = geoPos.X; // X je Longitude
            wp.altitude = geoPos.Z;  // Z je Altitude

            gpsPath.Add(wp);
        }

        return gpsPath;
    }

    // Samostatná funkce pro vizualizaci (abychom ji mohli volat i z Controlleru)
    public void VisualizePath(List<Vector3> path) {
        if (path.Count == 0)
            return;

        lineRenderer.positionCount = path.Count;
        lineRenderer.SetPositions(path.ToArray());

        for (int i = 0; i < path.Count; i++) {
            Vector3 currentPos = path[i];
            CreateWaypointMarker(currentPos, i);
            if (use3DTubes && i < path.Count - 1) {
                CreateTubeSegment(currentPos, path[i + 1]);
            }
        }
    }

    private void CreateWaypointMarker(Vector3 pos, int index) {
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
        int layerIndex = LayerMask.NameToLayer("Buildings");
        wpObj.layer = (layerIndex != -1) ? layerIndex : 0;
        spawnedObjects.Add(wpObj);
    }

    private void CreateTubeSegment(Vector3 start, Vector3 end) {
        GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tube.name = "Tube_Segment";
        int layerIndex = LayerMask.NameToLayer("Buildings");
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
    }

    public void ClearPath() {
        lineRenderer.positionCount = 0;
        foreach (var obj in spawnedObjects) {
            if (obj != null)
                Destroy(obj);
        }
        spawnedObjects.Clear();
    }
}
