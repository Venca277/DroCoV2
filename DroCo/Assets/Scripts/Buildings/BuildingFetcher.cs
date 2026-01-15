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
    public DroneMissionController missionController;

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
        Vector3 centroidSeaLevel = Vector3.zero;
        foreach (var wp in worldPoints)
            centroidSeaLevel += wp;
        centroidSeaLevel /= worldPoints.Count;

        // 2. Geo souřadnice a Pivot
        ArcGISPoint centroidGeo = map.EngineToGeographic(centroidSeaLevel);
        ArcGISPoint pivotGeo = new ArcGISPoint(centroidGeo.X, centroidGeo.Y, roofAltitudeFromRay, centroidGeo.SpatialReference);
        Vector3 pivotWorldPosition = map.GeographicToEngine(pivotGeo);

        // --- TVORBA HLAVNÍHO OBJEKTU ---
        currentSelection = new GameObject($"OSM_Selection_{buildingData.id}");
        GameObject buildingObj = currentSelection;
        if (map != null)
            buildingObj.transform.SetParent(map.transform, true);

        buildingObj.transform.position = pivotWorldPosition;

        int layerIndex = LayerMask.NameToLayer("Buildings");
        buildingObj.layer = (layerIndex != -1) ? layerIndex : 0;

        var locationComponent = buildingObj.AddComponent<ArcGISLocationComponent>();
        locationComponent.Position = pivotGeo;
        locationComponent.Rotation = new ArcGISRotation(0, 90, 0);
        locationComponent.enabled = true;

        var mf = buildingObj.AddComponent<MeshFilter>();
        var mr = buildingObj.AddComponent<MeshRenderer>();
        // Poznámka: Materiál pro budovu neřešíme, protože ji stejně zneviditelníme.

        // --- MESH GENERACE (Původní kód beze změny) ---
        Mesh mesh = new Mesh();
        if (worldPoints.Count * 2 > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        int n = worldPoints.Count;
        Vector3[] verts = new Vector3[n * 2];

        float osmHeight = GetRealHeight(buildingData);
        if (osmHeight < 5f)
            osmHeight = 15f;

        float topY = 0.5f;
        float bottomY = -osmHeight;
        float inflation = 0.1f;

        for (int i = 0; i < n; i++) {
            Vector3 offset = worldPoints[i] - centroidSeaLevel;
            Vector3 local = new Vector3(offset.x, 0, offset.z);
            Vector3 dir = local.normalized;
            Vector3 inflated = local + (dir * inflation);

            verts[i] = new Vector3(inflated.x, bottomY, inflated.z);
            verts[i + n] = new Vector3(inflated.x, topY, inflated.z);
        }

        mesh.vertices = verts;

        List<int> tris = new List<int>();
        int off = n;
        for (int i = 1; i < n - 1; i++) {
            tris.Add(off);
            tris.Add(off + i);
            tris.Add(off + i + 1);
        }
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

        // ==========================================
        // === ZDE ZAČÍNÁ OPRAVA (HIGHLIGHT) ===
        // ==========================================

        // 1. DUCH BUDOVY - Uděláme ho neviditelným (ale klikatelným)
        // Místo řešení průhlednosti ho prostě vypneme vykreslování.
        mr.enabled = false;

        // 2. PODLAHA - KRUH (Válec)
        GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floorObj.name = "Floor_Highlight";
        floorObj.transform.SetParent(buildingObj.transform, false);

        // ZAROVNÁNÍ NA STŘED:
        // mesh.bounds.center nám řekne, kde je optický střed budovy vůči pivotu.
        // bottomY + 1.0f zajistí, že to bude metr ode dna (aby to neblikalo v zemi).
        Vector3 center = mesh.bounds.center;
        floorObj.transform.localPosition = new Vector3(center.x, bottomY + 1.0f, center.z);
        floorObj.transform.localRotation = Quaternion.identity; // Válec stojí, to je OK pro kruh

        // VELIKOST KRUHU:
        Bounds b = mesh.bounds;
        float maxSize = Mathf.Max(b.size.x, b.size.z);
        float diameter = maxSize * 1.3f; // 1.3x větší než budova (mírně větší)

        // Scale: X, Z = průměr. Y = výška (splácneme ho na placku 0.05)
        floorObj.transform.localScale = new Vector3(diameter, 0.05f, diameter);

        // ODSTRANIT KOLIZI VÁLCE (aby nepřekážela)
        Destroy(floorObj.GetComponent<Collider>());

        // MATERIÁL A ZÁŘE (GLOW)
        var floorMr = floorObj.GetComponent<MeshRenderer>();

        // Použijeme "Standard" shader, protože ten funguje vždycky a neudělá fialovou chybu
        Shader floorShader = Shader.Find("Universal Render Pipeline/Lit");
        if (floorShader == null) {
            floorShader = Shader.Find("Universal Render Pipeline/UnLit");
        }

        Material floorMat = new Material(floorShader);
        floorMat.SetColor("_BaseColor", new Color(0f, 1f, 0f, 0.05f));
        floorMat.SetFloat("_Smoothness", 0.0f);
        floorMat.EnableKeyword("_EMISSION");
        floorMat.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * 0.5f);
        floorMat.SetFloat("_Surface", 1);
        floorMat.SetFloat("_Blend", 0);
        floorMat.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.One);
        floorMat.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.One);
        floorMat.SetFloat("_ZWrite", 0);
        floorMat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
        floorMat.SetFloat("_Cull", (float) UnityEngine.Rendering.CullMode.Off);
        floorMat.SetInt("_ZTest", (int) UnityEngine.Rendering.CompareFunction.LessEqual);
        // Nastavíme průhledný režim (Fade/Transparent)
        //floorMat.SetFloat("_Mode", 2); // 2 = Fade
        //floorMat.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.SrcAlpha);
        //floorMat.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        //floorMat.SetInt("_ZWrite", 0);
        //floorMat.DisableKeyword("_ALPHATEST_ON");
        //floorMat.EnableKeyword("_ALPHABLEND_ON");
        //floorMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        //floorMat.renderQueue = 3000;

        // Barva: Zelená, poloprůhledná
        //Color neonGreen = new Color(0f, 1f, 0f, 0.5f);
        //floorMat.color = neonGreen;

        // ZÁŘE (Emission) - Tady to "ohulíme"
        //floorMat.EnableKeyword("_EMISSION");
        // Násobíme 10x, aby to svítilo i v jasném dni
        //floorMat.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * 10.0f);

        floorMr.material = floorMat;
        floorMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ==========================================
        // === KONEC OPRAVY ===
        // ==========================================

        if (missionController != null) {
            missionController.ProcessMission(buildingObj, worldPoints);
        } else {
            Debug.LogWarning("Chybí DroneMissionController v Inspectoru!");
        }
    }
}
