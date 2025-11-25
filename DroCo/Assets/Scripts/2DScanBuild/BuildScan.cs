using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;

public class BuildingClickOblet : MonoBehaviour {
    [SerializeField] private Camera arcgisCamera;
    [SerializeField] private float radiusMeters = 15f;
    [SerializeField] private int numPoints = 6;
    [SerializeField] private float flightHeight = 10f; // relative to hit point Y

    [SerializeField] private GameObject waypointPrefab;
    [SerializeField] private Transform missionParent;
    [SerializeField] private Material material;

    // Debug / runtime material overrides
    [SerializeField] private bool enableDebugMarkers = true;
    private Material runtimeWaypointMaterial;
    private Material runtimeLineMaterial;

    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.25f;

    private List<GameObject> waypointInstances = new List<GameObject>();
    private GameObject currentLineObject;

    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            float timeSinceLastClick = Time.time - lastClickTime;
            lastClickTime = Time.time;
            if (timeSinceLastClick <= doubleClickThreshold) {
                Ray ray = arcgisCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f)) {
                    Vector3 center = hit.point;
                    GenerateCircularMission(center);
                }
            }
        }
    }

    void GenerateCircularMission(Vector3 center) {
        // Destroy previous waypoints and debug markers
        foreach (var wp in waypointInstances)
            Destroy(wp);
        waypointInstances.Clear();

        // Destroy previous line
        if (currentLineObject != null)
            Destroy(currentLineObject);

        // Prepare runtime materials (do not modify project assets)
        EnsureRuntimeMaterials();

        // Create LineRenderer object
        currentLineObject = new GameObject("WaypointLine");
        currentLineObject.transform.parent = missionParent;
        LineRenderer line = currentLineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true; // important: keep world-space positions so depth testing is correct
        line.material = runtimeLineMaterial;
        line.startWidth = 0.2f;
        line.endWidth = 0.2f;
        line.positionCount = numPoints + 1;
        line.loop = true;
        line.alignment = LineAlignment.View; // keep line visible from camera angle (still depth-tested)

        float angleStep = 360f / numPoints;
        for (int i = 0; i < numPoints; i++) {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float dx = Mathf.Cos(angle) * radiusMeters;
            float dz = Mathf.Sin(angle) * radiusMeters;

            Vector3 wp = new Vector3(center.x + dx, center.y + flightHeight, center.z + dz);

            // Instantiate waypoint prefab (if provided) and override its material for correct depth behavior
            if (waypointPrefab) {
                GameObject instance = Instantiate(waypointPrefab, wp, Quaternion.identity, missionParent);

                // get renderer and replace material instance so it writes depth and uses opaque queue
                Renderer r = instance.GetComponent<Renderer>();
                if (r) {
                    // assign a new instance of runtimeWaypointMaterial so each marker can be adjusted independently
                    Material matInstance = new Material(runtimeWaypointMaterial);
                    matInstance.color = Color.red;
                    matInstance.renderQueue = (int) RenderQueue.Geometry;
                    r.material = matInstance;
                }

                waypointInstances.Add(instance);
            }

            // Optionally add small debug sphere so you can visually inspect exact world positions
            if (enableDebugMarkers) {
                GameObject dbg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dbg.name = "WaypointDebugMarker";
                dbg.transform.position = wp;
                dbg.transform.localScale = Vector3.one * 0.5f;
                dbg.transform.parent = missionParent;
                // remove collider to avoid physics interference
                Collider col = dbg.GetComponent<Collider>();
                if (col)
                    Destroy(col);

                Renderer dbgR = dbg.GetComponent<Renderer>();
                if (dbgR) {
                    Material dbgMat = new Material(runtimeWaypointMaterial);
                    dbgMat.color = new Color(1f, 1f, 0f, 1f); // yellow
                    dbgMat.renderQueue = (int) RenderQueue.Geometry;
                    dbgR.material = dbgMat;
                }

                waypointInstances.Add(dbg);
            }

            line.SetPosition(i, wp);

            // Debug log: camera distance and local Y for quick verification
            float camDist = (arcgisCamera.transform.position - wp).magnitude;
            Debug.Log($"Waypoint[{i}] worldPos={wp} camDist={camDist:F2} hitY={center.y:F2}");
        }

        // close the loop
        line.SetPosition(numPoints, line.GetPosition(0));

        Debug.Log("Generated " + numPoints + " red waypoints and connected them with a line (runtime materials applied).");
    }

    private void EnsureRuntimeMaterials() {
        // Create or clone runtime line material
        if (runtimeLineMaterial == null) {
            if (material != null) {
                // clone provided material to avoid editing asset
                runtimeLineMaterial = new Material(material);
            } else {
                // fallback: try URP Unlit, otherwise default shader
                Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
                runtimeLineMaterial = new Material(s);
            }

            // prefer opaque geometry queue so depth writes behave normally
            runtimeLineMaterial.renderQueue = (int) RenderQueue.Geometry;
            // try to enable ZWrite and standard ZTest - many URP shaders expose these keywords/properties
            if (runtimeLineMaterial.HasProperty("_ZWrite"))
                runtimeLineMaterial.SetInt("_ZWrite", 1);
            if (runtimeLineMaterial.HasProperty("_Surface")) {
                // Some URP shaders use _Surface to toggle Transparent/Opaque (0 = Opaque)
                runtimeLineMaterial.SetFloat("_Surface", 0f);
            }
            // ensure a visible color if possible
            if (runtimeLineMaterial.HasProperty("_BaseColor"))
                runtimeLineMaterial.SetColor("_BaseColor", Color.green);
            else if (runtimeLineMaterial.HasProperty("_Color"))
                runtimeLineMaterial.SetColor("_Color", Color.green);
        }

        // Create runtime waypoint material
        if (runtimeWaypointMaterial == null) {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            runtimeWaypointMaterial = new Material(s);
            runtimeWaypointMaterial.renderQueue = (int) RenderQueue.Geometry;
            if (runtimeWaypointMaterial.HasProperty("_ZWrite"))
                runtimeWaypointMaterial.SetInt("_ZWrite", 1);
            if (runtimeWaypointMaterial.HasProperty("_Surface"))
                runtimeWaypointMaterial.SetFloat("_Surface", 0f);
            if (runtimeWaypointMaterial.HasProperty("_BaseColor"))
                runtimeWaypointMaterial.SetColor("_BaseColor", Color.red);
            else if (runtimeWaypointMaterial.HasProperty("_Color"))
                runtimeWaypointMaterial.SetColor("_Color", Color.red);
        }
    }
}
