using System.Collections.Generic;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Geometry;
using UnityEngine;

public class BuildingFetcher : MonoBehaviour {
    public Camera arcgisCamera;
    public OverpassClient overpass;
    public ArcGISMapComponent map;

    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.25f;

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

            StartCoroutine(overpass.FetchBuildingData(lat, lon, (json) => {
                if (json == null) {
                    Debug.LogError("OSM fetch failed.");
                    return;
                }

                Debug.Log("JSON fetched successfully.");

                OSMRoot root = JsonUtility.FromJson<OSMRoot>(json);

                OSMElement building = OSMBuildingSelector.FindClosestBuilding(root, lat, lon);

                if (building == null) {
                    Debug.LogError("No building found near click.");
                    return;
                }

                Debug.Log($"Selected building ID: {building.id} | points: {building.geometry.Count}");

                var unityPoints = OSMToUnity.ConvertPolygonToUnity(building, map, geo.Z);

                foreach (var p in unityPoints) {
                    Debug.Log($"Unity world point: {p}");
                }

                Debug.Log("Building polygon converted to Unity coordinates.");
                CreateBuildingMesh(unityPoints);
            }));
        }
    }

    public void CreateBuildingMesh(List<Vector3> worldPoints) {
        if (worldPoints == null || worldPoints.Count < 3) {
            Debug.LogWarning("Polygon too small or null.");
            return;
        }

        Vector3 centroid = Vector3.zero;
        foreach (var wp in worldPoints)
            centroid += wp;
        centroid /= worldPoints.Count;

        GameObject building = new GameObject("OSM_Building");
        building.transform.position = centroid;
        building.layer = 0;

        var mf = building.AddComponent<MeshFilter>();
        var mr = building.AddComponent<MeshRenderer>();

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Material mat = new Material(unlit);
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_ZWrite"))
            mat.SetInt("_ZWrite", 1);
        mat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Geometry;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", new Color(0f, 0.4f, 1f, 1f));
        else
            mat.color = new Color(0f, 0.4f, 1f, 1f);

        mr.material = mat;
        mr.material.renderQueue = 4000;
        if (mr.material.HasProperty("_ZTest")) mr.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.allowOcclusionWhenDynamic = false;

        Mesh mesh = new Mesh();
        mesh.name = "OSM_Building_Mesh";

        int n = worldPoints.Count;
        if (n * 2 > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        Vector3[] verts = new Vector3[n * 2];
        for (int i = 0; i < n; i++) {
            Vector3 local = worldPoints[i] - centroid;
            verts[i] = local;
            verts[i + n] = local + new Vector3(0, 8f, 0);
        }

        mesh.vertices = verts;

        List<int> tris = new List<int>();
        for (int i = 1; i < n - 1; i++) {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i + 1);
        }

        int off = n;
        for (int i = 1; i < n - 1; i++) {
            tris.Add(off);
            tris.Add(off + i + 1);
            tris.Add(off + i);
        }

        for (int i = 0; i < n; i++) {
            int next = (i + 1) % n;

            int bottomA = i;
            int bottomB = next;
            int topA = i + off;
            int topB = next + off;

            tris.Add(bottomA);
            tris.Add(bottomB);
            tris.Add(topA);

            tris.Add(topA);
            tris.Add(bottomB);
            tris.Add(topB);
        }

        mesh.triangles = tris.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Vector2[] uvs = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            uvs[i] = new Vector2(verts[i].x, verts[i].z);
        mesh.uv = uvs;

        mf.mesh = mesh;

        Bounds localBounds = mesh.bounds;
        Bounds worldBounds = new Bounds(building.transform.TransformPoint(localBounds.center), Vector3.Scale(localBounds.size, building.transform.lossyScale));
        Debug.Log($"BUILDING CREATED: pos={building.transform.position} mesh.bounds.center(local)={localBounds.center} size={localBounds.size} worldBounds.center={worldBounds.center} size={worldBounds.size}");

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(arcgisCamera);
        bool inFrustum = GeometryUtility.TestPlanesAABB(planes, worldBounds);
        Debug.Log($"Frustum test: inFrustum={inFrustum} camPos={arcgisCamera.transform.position} camForward={arcgisCamera.transform.forward}");

        Debug.DrawLine(arcgisCamera.transform.position, centroid, Color.yellow, 5f);
        Debug.DrawRay(centroid, Vector3.up * 5f, Color.cyan, 5f);

        if (!inFrustum) {
            Debug.LogWarning("Building mesh is outside camera frustum.");
        }
        var col = building.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = false;

        Debug.Log("BUILDING CREATED in Unity scene! Centroid: " + centroid);
    }
}
