using System.Collections.Generic;
using System.Globalization;
using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Geometry;
using UnityEngine;
using System.Globalization;
using Newtonsoft.Json;
using System;

public class BuildingFetcher : MonoBehaviour {
    public Camera arcgisCamera;
    public OverpassClient overpass;
    public ArcGISMapComponent map;
    public MissionGenerator missionGenerator;

    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.25f;
    private GameObject currentSelection = null;

    private void Update() {
        if (Input.GetMouseButtonDown(0)) {
            float timeSinceLastClick = Time.time - lastClickTime;
            lastClickTime = Time.time;

            if (timeSinceLastClick > doubleClickThreshold)
                return;

            Ray ray = arcgisCamera.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
                return;

            ArcGISPoint geo = map.EngineToGeographic(hit.point);
            double lat = geo.Y;
            double lon = geo.X;

            Debug.Log($"DOUBLE-CLICK GEO: lat={lat}, lon={lon}");

            StartCoroutine(overpass.FetchBuildingData(lat, lon, (jsonString) => {
                if (string.IsNullOrEmpty(jsonString)) {
                    Debug.LogError("OSM fetch failed or empty.");
                    return;
                }

                // 1. DESERIALIZACE POMOCÍ NEWTONSOFT (místo JsonUtility)
                OSMRoot root = null;
                try {
                    root = JsonConvert.DeserializeObject<OSMRoot>(jsonString);
                } catch (System.Exception e) {
                    Debug.LogError($"JSON Parse Error: {e.Message}");
                    return;
                }

                if (root == null || root.elements == null)
                    return;

                // Najdeme nejbližší budovu
                OSMElement building = OSMBuildingSelector.FindClosestBuilding(root, lat, lon);

                if (building == null) {
                    Debug.LogError("No building found near click.");
                    return;
                }

                Debug.Log($"Selected building ID: {building.id} | Tags found: {building.tags?.Count ?? 0}");

                ArcGISPoint hitGeo = map.EngineToGeographic(hit.point);
                float roofAltitude = (float) hitGeo.Z;

                // 2. ZÍSKÁNÍ REÁLNÉ VÝŠKY Z TAGŮ
                float realHeight = GetRealHeight(building);
                Debug.Log($"Calculated Height: {realHeight}m");

                // 3. PŘEVOD BODŮ NA UNITY WORLD
                var unityPoints = OSMToUnity.ConvertPolygonToUnity(building, map, 0);

                // 4. VYKRESLENÍ MESH
                CreateBuildingMesh(unityPoints, building, roofAltitude);
            }));
        }
    }

    public void ClearSelection() {
        if (currentSelection != null) {
            Destroy(currentSelection);
            currentSelection = null;
        }
        if (missionGenerator != null)
            missionGenerator.ClearPath();
    }

    private float GetRealHeight(OSMElement building) {
        float defaultHeight = 15f; // Bezpečný default
        float floorHeight = 4.0f;  // Větší patra pro veřejné budovy

        if (building.tags == null)
            return defaultHeight;

        if (building.tags.TryGetValue("height", out string heightStr)) {
            heightStr = heightStr.Replace("m", "").Trim();
            if (float.TryParse(heightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float h)) {
                return h;
            }
        }

        if (building.tags.TryGetValue("building:levels", out string levelsStr)) {
            if (float.TryParse(levelsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float l)) {
                // Počet pater * 5m + 1m rezerva na atiku
                return (l * floorHeight) + 1.0f;
            }
        }

        return defaultHeight;
    }

    public void CreateBuildingMesh(List<Vector3> worldPoints, OSMElement buildingData, float roofAltitudeFromRay) {
        ClearSelection();

        if (worldPoints == null || worldPoints.Count < 3)
            return;

        // 1. Centroid (střed budovy PŮDORYSNĚ)
        // worldPoints jsou na hladině moře (protože jsme je tak převedli v Update), ale to nám nevadí pro X a Z.
        Vector3 centroidSeaLevel = Vector3.zero;
        foreach (var wp in worldPoints)
            centroidSeaLevel += wp;
        centroidSeaLevel /= worldPoints.Count;

        // 2. Zjistíme Geo souřadnice středu (Lat/Lon)
        ArcGISPoint centroidGeo = map.EngineToGeographic(centroidSeaLevel);

        // ZMĚNA: Pivot (Střed objektu) přesuneme NA STŘECHU (tam, kam jsme klikli)
        // Tím zajistíme, že se s objektem bude dobře manipulovat a bude sedět v prostoru.
        ArcGISPoint pivotGeo = new ArcGISPoint(centroidGeo.X, centroidGeo.Y, roofAltitudeFromRay, centroidGeo.SpatialReference);
        Vector3 pivotWorldPosition = map.GeographicToEngine(pivotGeo);


        // --- TVORBA OBJEKTU ---
        currentSelection = new GameObject($"OSM_Selection_{buildingData.id}");
        GameObject buildingObj = currentSelection;
        if (map != null)
            buildingObj.transform.SetParent(map.transform, true);

        // Nastavíme pozici objektu na STŘECHU
        buildingObj.transform.position = pivotWorldPosition;

        int layerIndex = LayerMask.NameToLayer("Buildings");
        buildingObj.layer = (layerIndex != -1) ? layerIndex : 0;

        // ArcGIS Location - kotvíme na STŘEŠE (Altitude = roofAltitudeFromRay)
        var locationComponent = buildingObj.AddComponent<ArcGISLocationComponent>();
        locationComponent.Position = pivotGeo;
        locationComponent.Rotation = new ArcGISRotation(0, 90, 0);
        locationComponent.enabled = true;

        var mf = buildingObj.AddComponent<MeshFilter>();
        var mr = buildingObj.AddComponent<MeshRenderer>();

        // --- MATERIÁL (ZELENÝ HOLOGRAM) ---
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0f, 1f, 0f, 0.05f));
        mat.SetFloat("_Smoothness", 0.0f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * 3.0f); // Intenzita 3

        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0);
        mat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetFloat("_Cull", (float) UnityEngine.Rendering.CullMode.Off);
        mat.SetInt("_ZTest", (int) UnityEngine.Rendering.CompareFunction.LessEqual);
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // --- MESH GENERACE ---
        Mesh mesh = new Mesh();
        if (worldPoints.Count * 2 > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        int n = worldPoints.Count;
        Vector3[] verts = new Vector3[n * 2];

        // ZMĚNA VÝŠKY:
        // Protože Pivot (0,0,0 lokálně) je na STŘEŠE:
        // Top = 0.5m nad střechu.
        // Bottom = -Height pod střechu.

        float osmHeight = GetRealHeight(buildingData);

        // Pojistka: Pokud OSM neví výšku (vrací default), dáme tam raději fixních 15m, 
        // aby to nevypadalo jako malá placka.
        // Zvýšíme trochu výšku pater, pro školy je 3.5m málo.
        if (osmHeight < 5f)
            osmHeight = 15f;

        float topY = 0.5f;
        float bottomY = -osmHeight; // Stavíme dolů do hloubky
        float inflation = 0.1f;

        for (int i = 0; i < n; i++) {
            // worldPoints jsou na moři, pivot je na střeše.
            // Musíme vypočítat horizontální posun (X, Z) bez ohledu na výšku.
            // Protože Unity Y je nahoru, rozdíl worldPoints[i] - centroidSeaLevel nám dá správné X/Z offsety.
            Vector3 offset = worldPoints[i] - centroidSeaLevel;

            Vector3 local = new Vector3(offset.x, 0, offset.z); // Y ignorujeme, řešíme ho přes topY/bottomY
            Vector3 dir = local.normalized;
            Vector3 inflated = local + (dir * inflation);

            verts[i] = new Vector3(inflated.x, bottomY, inflated.z);
            verts[i + n] = new Vector3(inflated.x, topY, inflated.z);
        }

        mesh.vertices = verts;

        List<int> tris = new List<int>();
        int off = n;
        // Střecha
        for (int i = 1; i < n - 1; i++) {
            tris.Add(off);
            tris.Add(off + i);
            tris.Add(off + i + 1);
        }
        // Stěny
        for (int i = 0; i < n; i++) {
            int next = (i + 1) % n;
            tris.Add(i);
            tris.Add(i + off);
            tris.Add(next);
            tris.Add(next);
            tris.Add(i + off);
            tris.Add(next + off);
            tris.Add(next);
            tris.Add(i + off);
            tris.Add(i);
            tris.Add(next + off);
            tris.Add(i + off);
            tris.Add(next);
        }

        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.mesh = mesh;

        var col = buildingObj.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;

        if (missionGenerator != null) {
            // Musíme poslat worldPoints (půdorys), které už máme v této funkci k dispozici
            missionGenerator.GenerateScanPath(buildingObj, worldPoints);
        }
    }
}
