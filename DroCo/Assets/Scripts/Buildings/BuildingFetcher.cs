using System.Collections.Generic;
using System.Globalization;
using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Geometry;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.EventSystems;
using System.Collections;

public class BuildingFetcher : MonoBehaviour {
    public Camera arcgisCamera;
    public OverpassClient overpass;
    public ArcGISMapComponent map;
    public MissionGenerator missionGenerator;
    public MissionController missionController;
    public Settings settings;
    public bool showGhost = true;

    private float lastClick = 0f;
    private float doubleClickTime = 0.25f;
    private float highAlt = 50000f;
    private float maxDist = 100000f;
    private float defHeight = 15f; //average building height
    private float lvlHeight = 4.0f;  //default floor height
    private float buildingHeight = 0f;
    private Shader shader;
    private Material buildingMat;
    private Material floorMat;
    private OSMElement lastBuilding = null;
    private List<Vector3> lastPoints = null;
    private GameObject currentSelection = null;
    private List<Vector3> buildingWorldPoints = null;
    private List<GameObject> buildings = new List<GameObject>();

    private void Start() {
        if (settings.range != "none") {
            WaitForSeconds wait = new WaitForSeconds(15f);
            StartCoroutine(FetchArea(wait));
        }
        shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) {
            shader = Shader.Find("Universal Render Pipeline/UnLit");
        }

        buildingMat = new Material(shader);
        buildingMat.SetColor("_BaseColor", new Color(0f, 0f, 1f, 1f));
        buildingMat.SetFloat("_Smoothness", 0.0f);
        buildingMat.SetFloat("_Surface", 0);
        buildingMat.SetFloat("_ZWrite", 1);
        buildingMat.SetFloat("_Cull", (float) UnityEngine.Rendering.CullMode.Off);
        buildingMat.SetInt("_ZTest", (int) UnityEngine.Rendering.CompareFunction.LessEqual);

        floorMat = new Material(shader);
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
    }

    private void Update() {
        //double click detection
        if (Input.GetMouseButtonDown(0)) {
            float timeSinceLastClick = Time.time - lastClick;
            lastClick = Time.time;

            //threshold check
            if (timeSinceLastClick > doubleClickTime)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
                return;
            }

            //send ray to detect if any already loaded building is in scene
            //also gets ground hit if nothings is preloaded 
            Ray ray = arcgisCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
            BuildingTag foundLoaded = null;
            Vector3 point = Vector3.zero;
            foreach (RaycastHit hit in hits) {
                foundLoaded = hit.collider.gameObject.GetComponent<BuildingTag>();
                if (point == Vector3.zero || point.y > hit.point.y)
                    point = hit.point;
                if (foundLoaded != null) {
                    break;
                }
            }

            if (foundLoaded != null) {
                ClearSelection();
                currentSelection = foundLoaded.gameObject;
                buildingWorldPoints = foundLoaded.WorldPoints;
                MeshRenderer render = currentSelection.GetComponent<MeshRenderer>();
                if (render != null)
                    render.enabled = showGhost;

                FloorSelect(currentSelection, currentSelection.GetComponent<MeshFilter>().mesh, currentSelection.GetComponent<MeshFilter>().mesh.bounds.min.y);
                missionController.PrepareMission(currentSelection, buildingWorldPoints);
                MissionUI.Instance?.SetNewMission(currentSelection, buildingWorldPoints);
                return;
            }

            //convert to geocoords
            ArcGISPoint geo = map.EngineToGeographic(point);
            double lat = geo.Y;
            double lon = geo.X;

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
                    Toast.call.Show("Unexpected JSON format", 2f, true);
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

                //recalculating world point to relative unity coordinates
                List<Vector3> unityRel = OSMToUnity.ConvertPolygonToUnity(building, map, 0);

                //reconstruction of unity building object
                CreateBuildingMesh(unityRel, building);
            }));
        }
    }

    public void ClearSelection() {
        if (currentSelection != null) {
            buildings.Remove(currentSelection);
            Destroy(currentSelection);
            currentSelection = null;
        }
        if (missionGenerator != null)
            missionGenerator.ClearPath();
    }

    private IEnumerator FetchArea(WaitForSeconds wait) {
        yield return wait;

        ArcGISPoint cam = map.EngineToGeographic(arcgisCamera.transform.position);
        double lat = cam.Y;
        double lon = cam.X;

        //remove old buildings
        List<GameObject> rem = new List<GameObject>();
        foreach (GameObject build in buildings) {
            if (build != currentSelection)
                rem.Add(build);
        }
        foreach (GameObject build in rem) {
            buildings.Remove(build);
            Destroy(build);
        }

        StartCoroutine(overpass.FetchBuildingData(lat, lon, (jsonString) => {
            if (string.IsNullOrEmpty(jsonString)) {
                Debug.LogError("OSM fetch failed or empty.");
                return;
            }

            //json deserialize
            OSMRoot root;
            try {
                root = JsonConvert.DeserializeObject<OSMRoot>(jsonString);
            } catch (System.Exception e) {
                Debug.LogError($"JSON Parse Error: {e.Message}");
                Toast.call.Show("Unexpected JSON format", 2f, true);
                return;
            }

            if (root == null || root.elements == null)
                return;

            foreach (OSMElement building in root.elements) {
                List<Vector3> unityRel = OSMToUnity.ConvertPolygonToUnity(building, map, 0);
                CreateBuildingMesh(unityRel, building, false);
            }
            Toast.call.Show("Building models loaded", 2f, false);
        }, settings.getRange()));
    }

    private float GetAltitudeFromCast(Vector3 center) {
        Vector3 highPlace = new Vector3(center.x, highAlt, center.z);
        Ray down = new Ray(highPlace, Vector3.down);

        int layer = LayerMask.GetMask("Default");
        RaycastHit[] hits = Physics.RaycastAll(down, maxDist, layer);
        if (hits.Length == 0) {
            Debug.LogWarning("No hit on ground!");
            return 0f;
        }

        RaycastHit lowest = hits[0];
        float lowestY = lowest.point.y;

        foreach (RaycastHit hit in hits) {
            if (hit.point.y < lowestY) {
                lowest = hit;
                lowestY = hit.point.y;
            }
        }

        ArcGISPoint ground = map.EngineToGeographic(lowest.point);
        float alt = (float) ground.Z;
        return alt;
    }

    private float GetHeight(OSMElement building) {
        float osmHeight = 0f;

        if (buildingHeight <= 0f && building.tags != null) {
            //extract height from json object tags
            if (building.tags.TryGetValue("height", out string heightStr)) {
                heightStr = heightStr.Replace("m", "").Trim();
                if (float.TryParse(heightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float height)) {
                    return height;
                }
            }

            //calculate height from levels and average floor height
            if (building.tags.TryGetValue("building:levels", out string levelsStr)) {
                if (float.TryParse(levelsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float lvl)) {
                    return (lvl * lvlHeight) + 1.0f;
                }
            }
        } else
            osmHeight = buildingHeight;

        //fallback for any invalid height
        if (osmHeight < 1f)
            osmHeight = 15f;

        return osmHeight;
    }

    public void CreateBuildingMesh(List<Vector3> worldPoints, OSMElement buildingData, bool generateMission = true) {
        if (generateMission)
            ClearSelection();

        if (worldPoints == null || worldPoints.Count < 3)
            return;

        // footprint center
        Vector3 center = missionGenerator.GetMissionCenter(worldPoints);
        float gndAlt = GetAltitudeFromCast(center);

        //determine building height
        float osmHeight = GetHeight(buildingData);
        lastBuilding = buildingData;
        lastPoints = worldPoints;

        float roof = gndAlt + osmHeight;

        //geolocation of the object
        ArcGISPoint objMid = map.EngineToGeographic(center);
        ArcGISPoint objPiv = new ArcGISPoint(objMid.X, objMid.Y, roof, objMid.SpatialReference);
        Vector3 relative = map.GeographicToEngine(objPiv);

        //gameobject reconstruction
        if (generateMission)
            currentSelection = new GameObject($"OSM_Selection_{buildingData.id}");

        GameObject buildingObj = null;
        if (generateMission) {
            buildingObj = currentSelection;
        } else
            buildingObj = new GameObject($"OSM_Building_{buildingData.id}");

        if (map != null)
            buildingObj.transform.SetParent(map.transform, true);

        //place the object at relative position
        buildingObj.transform.position = relative;

        int layer = LayerMask.NameToLayer("Buildings");
        if (layer != -1)
            buildingObj.layer = layer;
        else
            buildingObj.layer = 0;

        ArcGISLocationComponent locComp = buildingObj.AddComponent<ArcGISLocationComponent>();
        locComp.Position = objPiv;
        locComp.Rotation = new ArcGISRotation(0, 90, 0);
        locComp.enabled = true;

        MeshFilter filter = buildingObj.AddComponent<MeshFilter>();
        MeshRenderer render = buildingObj.AddComponent<MeshRenderer>();

        Mesh ghost = CreateBuildingGhost(worldPoints, center, osmHeight);
        filter.mesh = ghost;

        MeshCollider col = buildingObj.AddComponent<MeshCollider>();
        col.sharedMesh = ghost;
        col.convex = false;

        //enable the ghost
        render.enabled = showGhost;
        render.sharedMaterial = buildingMat;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (generateMission)
            FinishSelection(buildingObj, ghost, worldPoints, osmHeight);
        else
            AddAreaBuildings(buildingObj, worldPoints, buildingData);
    }

    private void FinishSelection(GameObject building, Mesh ghost, List<Vector3> pts, float height) {
        FloorSelect(building, ghost, -height);
        currentSelection = building;
        buildingWorldPoints = pts;
        buildings.Add(building);

        //hand over to next step
        missionController.PrepareMission(currentSelection, buildingWorldPoints);
        MissionUI.Instance?.SetNewMission(currentSelection, buildingWorldPoints, null, height);
    }

    private void AddAreaBuildings(GameObject obj, List<Vector3> pts, OSMElement data) {
        BuildingTag tag = obj.AddComponent<BuildingTag>();
        tag.BuildingData = data;
        tag.WorldPoints = pts;
        buildings.Add(obj);
    }

    private Mesh CreateBuildingGhost(List<Vector3> worldPoints, Vector3 center, float height) {
        Mesh mesh = new Mesh();
        Vector3[] verts = new Vector3[worldPoints.Count * 2];

        float topY = 0.5f;
        float downY = -height;
        float expand = 0.1f;

        for (int i = 0; i < worldPoints.Count; i++) {
            Vector3 offset = worldPoints[i] - center;
            Vector3 local = new Vector3(offset.x, 0, offset.z);
            Vector3 dir = local.normalized;
            Vector3 inflated = local + (dir * expand);

            verts[i] = new Vector3(inflated.x, downY, inflated.z);
            verts[i + worldPoints.Count] = new Vector3(inflated.x, topY, inflated.z);
        }

        mesh.vertices = verts;

        List<int> tris = new List<int>();
        int off = worldPoints.Count;

        tris.AddRange(RoofTriang(verts, off, worldPoints.Count));
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

        return mesh;
    }



    public void RegenerateBuilding(float height) {
        buildingHeight = height;
        ClearSelection();
        CreateBuildingMesh(lastPoints, lastBuilding, true);
    }

    private void FloorSelect(GameObject buildingObj, Mesh mesh, float bottomY) {
        Bounds b = mesh.bounds;
        b.center = new Vector3(b.center.x, bottomY + b.extents.y, b.center.z);
        FloorBoundsSelect(buildingObj, b);
    }

    public void FloorBoundsSelect(GameObject buildingObj, Bounds bounds) {
        // highlight floor object
        GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floorObj.name = "Floor_Highlight";
        floorObj.transform.SetParent(buildingObj.transform, false);

        // center alignment
        // mesh center is optical center of object
        // the height of the highlight above ground
        Vector3 center = bounds.center;
        floorObj.transform.localPosition = new Vector3(center.x, bounds.min.y, center.z);
        floorObj.transform.localRotation = Quaternion.identity;

        // size and dimensions of the floor highlight
        float maxSize = Mathf.Max(bounds.size.x, bounds.size.z);
        float diameter = maxSize * 1.3f;
        floorObj.transform.localScale = new Vector3(diameter, 0.05f, diameter);

        // remove collider
        Destroy(floorObj.GetComponent<Collider>());

        // material glow
        MeshRenderer floorMr = floorObj.GetComponent<MeshRenderer>();

        floorMr.sharedMaterial = floorMat;
        floorMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void SetCurrentSelection(GameObject obj) {
        currentSelection = obj;
    }

    private List<int> RoofTriang(Vector3[] verts, int startIdx, int count) {
        var res = new List<int>();
        var indices = new List<int>();
        for (int i = 0; i < count; i++)
            indices.Add(i);

        //polygon detection on plane
        float area = 0f;
        for (int i = 0; i < count; i++) {
            int j = (i + 1) % count;
            area += (verts[startIdx + i].x * verts[startIdx + j].z) - (verts[startIdx + j].x * verts[startIdx + i].z);
        }
        if (area < 0f)
            indices.Reverse();

        int safe = count * count + count;
        while (indices.Count > 3 && safe-- > 0) {
            bool clipFound = false;
            for (int i = 0; i < indices.Count; i++) {
                int iPrev = (i - 1 + indices.Count) % indices.Count;
                int iNext = (i + 1) % indices.Count;

                Vector3 a = verts[startIdx + indices[iPrev]];
                Vector3 b = verts[startIdx + indices[i]];
                Vector3 c = verts[startIdx + indices[iNext]];

                //convex
                float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
                if (cross <= 0f)
                    continue;

                //no other point clipping
                bool isEar = true;
                for (int j = 0; j < indices.Count; j++) {
                    if (j == iPrev || j == i || j == iNext)
                        continue;
                    if (PointInTri(a, b, c, verts[startIdx + indices[j]])) {
                        isEar = false;
                        break;
                    }
                }
                if (!isEar)
                    continue;

                res.Add(startIdx + indices[iPrev]);
                res.Add(startIdx + indices[i]);
                res.Add(startIdx + indices[iNext]);
                indices.RemoveAt(i);
                clipFound = true;
                break;
            }
            if (!clipFound)
                break;
        }

        if (indices.Count == 3) {
            res.Add(startIdx + indices[0]);
            res.Add(startIdx + indices[1]);
            res.Add(startIdx + indices[2]);
        }
        return res;
    }

    private bool PointInTri(Vector3 a, Vector3 b, Vector3 c, Vector3 p) {
        float d1 = (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.z - b.z);
        float d2 = (p.x - c.x) * (b.z - c.z) - (b.x - c.x) * (p.z - c.z);
        float d3 = (p.x - a.x) * (c.z - a.z) - (c.x - a.x) * (p.z - a.z);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }

    public void SetShowGhost(bool value) {
        showGhost = value;
        foreach (GameObject build in buildings) {
            MeshRenderer mr = build.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.enabled = showGhost;
        }
    }

    private void OnDestroy() {
        if (buildingMat != null)
            Destroy(buildingMat);
        if (floorMat != null)
            Destroy(floorMat);
    }
}
