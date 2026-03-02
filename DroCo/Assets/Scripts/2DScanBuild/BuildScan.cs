using System.Collections.Generic;
using Esri.GameEngine.Map;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingClickOblet : MonoBehaviour {
    [SerializeField] private Camera arcgisCamera;
    [SerializeField] private float radiusMeters = 15f;
    [SerializeField] private int numPoints = 6;
    [SerializeField] private float flightHeight = 10f;

    [SerializeField] private GameObject waypointPrefab;
    [SerializeField] private Transform missionParent;

    [Header("Occlusion")]
    public LayerMask occlusionMask;

    private float lastClick = 0f;
    private float doubleClickTime = 0.25f;

    private List<Renderer> waypointRenderers = new List<Renderer>();
    private List<(Vector3 a, Vector3 b, LineRenderer lr)> lines = new List<(Vector3, Vector3, LineRenderer)>();

    private List<GameObject> occluders = new List<GameObject>();


    void Update() {
        if (Input.GetMouseButtonDown(0)) {

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
                return;
            }
            float delta = Time.time - lastClick;
            lastClick = Time.time;

            if (delta <= doubleClickTime) {
                Ray ray = arcgisCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 500f)) {
                    //CreateOccluderBoxFromHit(hit);

                    //GenerateCircularMission(hit.point);
                }
            }
        }
    }

    void GenerateCircularMission(Vector3 center) {
        foreach (var r in waypointRenderers)
            Destroy(r.gameObject);

        foreach (var line in lines)
            Destroy(line.lr.gameObject);

        waypointRenderers.Clear();
        lines.Clear();

        float angleStep = 360f / numPoints;
        Vector3[] pts = new Vector3[numPoints + 1];

        for (int i = 0; i < numPoints; i++) {
            float rad = i * angleStep * Mathf.Deg2Rad;
            pts[i] = new Vector3(
                center.x + Mathf.Cos(rad) * radiusMeters,
                center.y + flightHeight,
                center.z + Mathf.Sin(rad) * radiusMeters
            );

            GameObject wp = Instantiate(waypointPrefab, pts[i], Quaternion.identity, missionParent);
            Renderer r = wp.GetComponentInChildren<Renderer>();
            if (r)
                waypointRenderers.Add(r);
        }

        pts[numPoints] = pts[0];

        for (int i = 0; i < numPoints; i++) {
            GameObject segObj = new GameObject("LineSeg_" + i);
            segObj.transform.parent = missionParent;

            LineRenderer lr = segObj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lr.material.color = Color.green;
            lr.startWidth = lr.endWidth = 0.2f;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.SetPosition(0, pts[i]);
            lr.SetPosition(1, pts[i + 1]);

            lines.Add((pts[i], pts[i + 1], lr));
        }
    }

    void LateUpdate() {
        Vector3 cam = arcgisCamera.transform.position;

        // waypoints
        foreach (var r in waypointRenderers) {
            if (r == null)
                continue;

            Vector3 p = r.transform.position;
            Vector3 dir = p - cam;
            float dist = dir.magnitude;

            bool hide = Physics.Raycast(cam, dir.normalized, dist - 0.1f, occlusionMask);
            r.enabled = !hide;
        }

        foreach (var seg in lines) {
            if (seg.lr == null)
                continue;

            Vector3 mid = (seg.a + seg.b) / 2f;
            Vector3 dir = mid - cam;
            float dist = dir.magnitude;

            bool hide = Physics.Raycast(cam, dir.normalized, dist - 0.1f, occlusionMask);
            seg.lr.enabled = !hide;
        }
    }
    void CreateOccluderBoxFromHit(RaycastHit hit) {
        Vector3 origin = hit.point;

        float maxDist = 40f;
        LayerMask mask = LayerMask.GetMask("Buildings");

        List<Vector3> pts = new List<Vector3>();

        for (int i = 0; i < 36; i++) {
            float ang = i * 10f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));

            if (Physics.Raycast(origin, dir, out RaycastHit h, maxDist, mask)) {
                pts.Add(h.point);
            }
        }

        float minY = origin.y;
        float maxY = origin.y;

        for (float y = -1f; y <= 1f; y += 2f) {
            if (Physics.Raycast(origin, new Vector3(0, y, 0), out RaycastHit h, 30f, mask)) {
                minY = Mathf.Min(minY, h.point.y);
                maxY = Mathf.Max(maxY, h.point.y);
            }
        }

        if (pts.Count == 0) {
            Debug.LogWarning("No contour points detected!");
            return;
        }

        float minX = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxZ = float.MinValue;

        foreach (var p in pts) {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
        }

        Vector3 center = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, (minZ + maxZ) / 2f);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);

        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "OccluderBox";
        box.transform.parent = missionParent;
        box.transform.position = center;
        box.transform.localScale = size;

        var rend = box.GetComponent<MeshRenderer>();
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = new Color(0, 0, 1, 0.25f);
        m.SetFloat("_Surface", 1);
        m.renderQueue = 3000;

        rend.material = m;

        box.layer = LayerMask.NameToLayer("Buildings");
    }
}
