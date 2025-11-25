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

            // pokud to nebyl double-click → ignorujeme
            if (timeSinceLastClick > doubleClickThreshold)
                return;

            // DOUBLE-CLICK
            Ray ray = arcgisCamera.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
                return;

            // World → Geo
            ArcGISPoint geo = map.EngineToGeographic(hit.point);
            double lat = geo.Y;
            double lon = geo.X;

            Debug.Log($"📍 DOUBLE-CLICK GEO: lat={lat}, lon={lon}");

            // Fetch OSM
            StartCoroutine(overpass.FetchBuildingData(lat, lon, (json) => {
                if (json == null) {
                    Debug.LogError("OSM fetch failed.");
                    return;
                }

                Debug.Log("📦 JSON fetched successfully.");

                // Parse
                OSMRoot root = JsonUtility.FromJson<OSMRoot>(json);

                // Select building
                OSMElement building = OSMBuildingSelector.FindClosestBuilding(root, lat, lon);

                if (building == null) {
                    Debug.LogError("No building found near click.");
                    return;
                }

                Debug.Log($"🏠 Selected building ID: {building.id} | points: {building.geometry.Count}");

                // Convert polygon → Unity world positions
                var unityPoints = OSMToUnity.ConvertPolygonToUnity(building, map);

                foreach (var p in unityPoints) {
                    Debug.Log($"Unity world point: {p}");
                }

                Debug.Log("✔ Building polygon converted to Unity coordinates.");
                CreateBuildingMesh(unityPoints);
            }));
        }
    }

    public void CreateBuildingMesh(List<Vector3> worldPoints) {
        if (worldPoints == null || worldPoints.Count < 3) {
            Debug.LogWarning("Polygon too small or null.");
            return;
        }

        // --- IMPORTANT FIXES ---
        // 1) Mesh vertices must have correct bounds for Unity frustum culling => call RecalculateBounds()
        // 2) Use local-space vertices + place GameObject at polygon centroid to avoid floating precision issues
        // 3) Disable backface culling or ensure normals orientation if polygon winding is unknown

        // compute centroid to use as GameObject position
        Vector3 centroid = Vector3.zero;
        foreach (var wp in worldPoints)
            centroid += wp;
        centroid /= worldPoints.Count;

        // create new GameObject and set its position to centroid
        GameObject building = new GameObject("OSM_Building");
        building.transform.position = centroid;

        var mf = building.AddComponent<MeshFilter>();
        var mr = building.AddComponent<MeshRenderer>();

        // simple material (URP Lit) - force opaque and disable culling so thin surfaces are visible
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // ensure opaque surface (if shader exposes _Surface)
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 0f);
        // disable backface culling if supported by shader (makes mesh visible regardless of winding)
        if (mat.HasProperty("_Cull"))
            mat.SetInt("_Cull", (int) UnityEngine.Rendering.CullMode.Off);
        // fallback: set color (try common properties)
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", new Color(0f, 0.4f, 1f, 0.9f));
        else
            mat.color = new Color(0f, 0.4f, 1f, 0.9f);

        mr.material = mat;

        // build mesh in local space (vertices = world - centroid)
        Mesh mesh = new Mesh();
        mesh.name = "OSM_Building_Mesh";

        int n = worldPoints.Count;
        Vector3[] verts = new Vector3[n * 2];
        for (int i = 0; i < n; i++) {
            Vector3 local = worldPoints[i] - centroid;
            verts[i] = local; // bottom
            verts[i + n] = local + new Vector3(0, 8f, 0); // top (extrude)
        }

        mesh.vertices = verts;

        // triangulate bottom (fan) — works only reliably for convex polygons
        List<int> tris = new List<int>();
        for (int i = 1; i < n - 1; i++) {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i + 1);
        }

        // triangulate top (reverse winding)
        int off = n;
        for (int i = 1; i < n - 1; i++) {
            tris.Add(off);
            tris.Add(off + i + 1);
            tris.Add(off + i);
        }

        // walls
        for (int i = 0; i < n; i++) {
            int next = (i + 1) % n;

            int bottomA = i;
            int bottomB = next;
            int topA = i + off;
            int topB = next + off;

            // first triangle
            tris.Add(bottomA);
            tris.Add(bottomB);
            tris.Add(topA);

            // second triangle
            tris.Add(topA);
            tris.Add(bottomB);
            tris.Add(topB);
        }

        mesh.triangles = tris.ToArray();

        // normals + bounds are crucial
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // optional: simple UVs so material shading behaves
        Vector2[] uvs = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            uvs[i] = new Vector2(verts[i].x, verts[i].z);
        mesh.uv = uvs;

        mf.mesh = mesh;

        // Optional: make building pickable / collideable by adding MeshCollider (optional)
        // var col = building.AddComponent<MeshCollider>();
        // col.sharedMesh = mesh;

        Debug.Log("✔ BUILDING CREATED in Unity scene! Centroid: " + centroid);
    }

}
