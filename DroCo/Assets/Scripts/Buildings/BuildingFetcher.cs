using System.Collections.Generic;
using System.Globalization;
using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Geometry;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using Newtonsoft.Json;
using System;

public class BuildingFetcher : MonoBehaviour {
    public Camera arcgisCamera;
    public OverpassClient overpass;
    public ArcGISMapComponent map;
    public MissionGenerator missionGenerator;
    public DroneMissionController missionController;
    //public Button launchMissionButton;
    public bool showGhost = false;

    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.25f;
    private GameObject currentSelection = null;

    private GameObject buildingObjectReady = null;
    private List<Vector3> buildingWorldPoints = null;

    private void Awake() {
        /*
        if (launchMissionButton != null) {
            launchMissionButton.interactable = false;
        }
        */
    }

    private void Update() {
        //double click detection
        if (Input.GetMouseButtonDown(0)) {
            float timeSinceLastClick = Time.time - lastClickTime;
            lastClickTime = Time.time;

            //threshold check
            if (timeSinceLastClick > doubleClickThreshold)
                return;

            //send ray
            Ray ray = arcgisCamera.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
                return;

            //convert to geocoordinates
            ArcGISPoint geo = map.EngineToGeographic(hit.point);
            double lat = geo.Y;
            double lon = geo.X;

            Debug.Log($"DOUBLE-CLICK GEO: lat={lat}, lon={lon}");

            StartCoroutine(overpass.FetchBuildingData(lat, lon, (jsonString) => {
                if (string.IsNullOrEmpty(jsonString)) {
                    Debug.LogError("OSM fetch failed or empty.");
                    return;
                }

                //json deserialize 
                OSMRoot root = null;
                try {
                    root = JsonConvert.DeserializeObject<OSMRoot>(jsonString);
                } catch (System.Exception e) {
                    Debug.LogError($"JSON Parse Error: {e.Message}");
                    return;
                }

                if (root == null || root.elements == null)
                    return;

                //elements json object of the closest building
                OSMElement building = OSMBuildingSelector.FindClosestBuilding(root, lat, lon);

                if (building == null) {
                    Debug.LogError("No building found near click.");
                    return;
                }

                Debug.Log($"Selected building ID: {building.id} | Tags found: {building.tags?.Count ?? 0}");

                //extract the Z value of user click
                ArcGISPoint hitGeo = map.EngineToGeographic(hit.point);
                float roofAltitude = (float) hitGeo.Z;

                //extract building height from OSM data if present or calculable
                float realHeight = GetRealHeight(building);
                Debug.Log($"Calculated Height: {realHeight}m");

                //recalculating world point to relative unity coordinates
                var unityRel = OSMToUnity.ConvertPolygonToUnity(building, map, 0);

                //reconstruction of unity building object
                CreateBuildingMesh(unityRel, building, roofAltitude);
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

    private float GetAltitudeFromCast(Vector3 center) {
        Vector3 highPlace = new Vector3(center.x, 42069f, center.z);
        Ray down = new Ray(highPlace, Vector3.down);

        int layer = LayerMask.GetMask("Default");
        RaycastHit[] hits = Physics.RaycastAll(down, 100000f, layer);
        if (hits.Length == 0) {
            Debug.LogWarning("No hit on ground!");
            return 0f;
        }

        RaycastHit lowest = hits[0];
        float lowestY = lowest.point.y;

        foreach (var hit in hits) {
            if (hit.point.y < lowestY) {
                lowest = hit;
                lowestY = hit.point.y;
            }
        }

        ArcGISPoint ground = map.EngineToGeographic(lowest.point);
        float alt = (float) ground.Z;
        Debug.Log($"Ground: {alt}m");
        return alt;
    }

    private float GetRealHeight(OSMElement building) {
        //TODO: think of more specific way to determine average floor height
        //perhaps get (hight of building arcgis model)/(floor count) = avg. floor height

        float defaultHeight = 15f; //average building height
        float floorHeight = 4.0f;  //default floor height

        if (building.tags == null)
            return defaultHeight;

        //extract height from json object tags
        //TODO: this might never be used, remove later ???
        if (building.tags.TryGetValue("height", out string heightStr)) {
            heightStr = heightStr.Replace("m", "").Trim();
            if (float.TryParse(heightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float h)) {
                return h;
            }
        }

        //calculate height from levels and average floor height
        if (building.tags.TryGetValue("building:levels", out string levelsStr)) {
            if (float.TryParse(levelsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float l)) {
                return (l * floorHeight) + 1.0f;
            }
        }

        return defaultHeight;
    }

    public void CreateBuildingMesh(List<Vector3> worldPoints, OSMElement buildingData, float roofAltitudeFromRay) {
        ClearSelection();

        if (worldPoints == null || worldPoints.Count < 3)
            return;

        // footprint center
        Vector3 centroidSeaLevel = Vector3.zero;
        foreach (var wp in worldPoints)
            centroidSeaLevel += wp;
        centroidSeaLevel /= worldPoints.Count;

        float groundAlt = GetAltitudeFromCast(centroidSeaLevel);
        Debug.Log("Ground: " + groundAlt);

        float osmHeight = GetRealHeight(buildingData);
        if (osmHeight < 5f)
            osmHeight = 15f;
        Debug.Log("OSM Height: " + osmHeight);


        float roof = groundAlt + osmHeight;

        // geolocation of the object
        ArcGISPoint objMid = map.EngineToGeographic(centroidSeaLevel);
        ArcGISPoint objPiv = new ArcGISPoint(objMid.X, objMid.Y, roof, objMid.SpatialReference);
        Vector3 relative = map.GeographicToEngine(objPiv);

        // gameobject reconstruction
        currentSelection = new GameObject($"OSM_Selection_{buildingData.id}");
        GameObject buildingObj = currentSelection;
        if (map != null)
            buildingObj.transform.SetParent(map.transform, true);

        //place the object at relative position
        buildingObj.transform.position = relative;

        int layer = LayerMask.NameToLayer("Buildings");
        //buildingObj.layer = (layer != -1) ? layer : 0;
        if (layer != -1)
            buildingObj.layer = layer;
        else
            buildingObj.layer = 0;

        var locationComponent = buildingObj.AddComponent<ArcGISLocationComponent>();
        locationComponent.Position = objPiv;
        locationComponent.Rotation = new ArcGISRotation(0, 90, 0);
        locationComponent.enabled = true;

        var mf = buildingObj.AddComponent<MeshFilter>();
        var mr = buildingObj.AddComponent<MeshRenderer>();

        //generation
        Mesh mesh = new Mesh();
        if (worldPoints.Count * 2 > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        //int n = worldPoints.Count;
        Vector3[] verts = new Vector3[worldPoints.Count * 2];


        float topY = 0.5f;
        float bottomY = -osmHeight;
        float inflation = 0.1f;

        for (int i = 0; i < worldPoints.Count; i++) {
            Vector3 offset = worldPoints[i] - centroidSeaLevel;
            Vector3 local = new Vector3(offset.x, 0, offset.z);
            Vector3 dir = local.normalized;
            Vector3 inflated = local + (dir * inflation);

            verts[i] = new Vector3(inflated.x, bottomY, inflated.z);
            verts[i + worldPoints.Count] = new Vector3(inflated.x, topY, inflated.z);
        }

        mesh.vertices = verts;

        List<int> tris = new List<int>();
        int off = worldPoints.Count;
        for (int i = 1; i < worldPoints.Count - 1; i++) {
            tris.Add(off);
            tris.Add(off + i);
            tris.Add(off + i + 1);
        }
        for (int i = 0; i < worldPoints.Count; i++) {
            int next = (i + 1) % worldPoints.Count;
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

        //enable the ghost
        mr.enabled = showGhost;

        //===========================================


        Shader buildingShader = Shader.Find("Universal Render Pipeline/Lit");
        if (buildingShader == null) {
            buildingShader = Shader.Find("Universal Render Pipeline/UnLit");
        }

        Material buildingMat = new Material(buildingShader);
        buildingMat.SetColor("_BaseColor", new Color(0f, 0f, 1f, 0.7f));
        buildingMat.SetFloat("_Smoothness", 0.0f);
        buildingMat.EnableKeyword("_EMISSION");
        buildingMat.SetColor("_EmissionColor", new Color(0f, 0f, 1f) * 1.5f);
        buildingMat.SetFloat("_Surface", 1);
        buildingMat.SetFloat("_Blend", 0);
        buildingMat.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.One);
        buildingMat.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.One);
        buildingMat.SetFloat("_ZWrite", 0);
        buildingMat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
        buildingMat.SetFloat("_Cull", (float) UnityEngine.Rendering.CullMode.Off);
        buildingMat.SetInt("_ZTest", (int) UnityEngine.Rendering.CompareFunction.LessEqual);

        mr.material = buildingMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ==========================================


        // highlight floor object
        GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floorObj.name = "Floor_Highlight";
        floorObj.transform.SetParent(buildingObj.transform, false);

        // center alignment
        // mesh center is optical center of object
        // the height of the highlight above ground
        Vector3 center = mesh.bounds.center;
        floorObj.transform.localPosition = new Vector3(center.x, bottomY + 1.0f, center.z);
        floorObj.transform.localRotation = Quaternion.identity;

        // size and dimensions of the floor highlight
        Bounds b = mesh.bounds;
        float maxSize = Mathf.Max(b.size.x, b.size.z);
        float diameter = maxSize * 1.3f;
        floorObj.transform.localScale = new Vector3(diameter, 0.05f, diameter);

        // remove collider
        Destroy(floorObj.GetComponent<Collider>());

        // material glow
        var floorMr = floorObj.GetComponent<MeshRenderer>();

        // universal material 
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

        floorMr.material = floorMat;
        floorMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // object created, saving for mission start
        buildingObjectReady = buildingObj;
        buildingWorldPoints = worldPoints;
        missionController.ProcessMission(buildingObjectReady, buildingWorldPoints);
        //UpdateButtonState();
        MissionUI.Instance?.SetNewMission(buildingObjectReady, buildingWorldPoints);
    }

    /*
    private void UpdateButtonState() {
        if (launchMissionButton != null) {
            launchMissionButton.interactable = true;
        }
    }
    */
}
