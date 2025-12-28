using System.Collections.Generic;
using UnityEngine;

public class MissionGenerator : MonoBehaviour {

    [Header("Parametry letu")]
    [Tooltip("Vzdálenost od stěny budovy (metry)")]
    public float scanDistance = 2.0f;

    [Tooltip("Výškový rozestup mezi závity (metry)")]
    public float verticalStep = 1.5f;

    [Header("Vzhled Trubky")]
    public bool use3DTubes = true; // Pokud false, použije se jen obyčejná čára
    public Color pathColor = Color.blue;
    public float tubeThickness = 0.2f; // Tloušťka trubky

    [Header("Waypointy")]
    [Tooltip("Zde přetáhněte prefab kuličky (nebo nechte prázdné)")]
    public GameObject waypointPrefab;
    public float waypointSize = 0.3f;

    // Interní
    private LineRenderer lineRenderer;
    // Seznam všech vytvořených objektů (kuličky i trubky), abychom je mohli smazat
    private List<GameObject> spawnedObjects = new List<GameObject>();

    void Awake() {
        // LineRenderer si necháme jako zálohu nebo pro "vnitřek" trubky
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // Vrstva
        int layerIndex = LayerMask.NameToLayer("Buildings");
        gameObject.layer = (layerIndex != -1) ? layerIndex : 0;

        // Nastavení LineRendereru (pro jistotu)
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

    public void GenerateScanPath(GameObject buildingObj, List<Vector3> footprintPoints) {
        ClearPath(); // Smazat staré

        if (buildingObj == null || footprintPoints == null || footprintPoints.Count < 3)
            return;

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

        // 3. Vykreslení (Trubky a Waypointy)
        if (finalPath.Count > 0) {

            // A) LineRenderer (rychlý náhled)
            lineRenderer.positionCount = finalPath.Count;
            lineRenderer.SetPositions(finalPath.ToArray());

            // B) 3D Trubky a Waypointy
            for (int i = 0; i < finalPath.Count; i++) {
                Vector3 currentPos = finalPath[i];

                // 1. Vytvořit Waypoint (Kuličku)
                CreateWaypointMarker(currentPos, i);

                // 2. Vytvořit Trubku k dalšímu bodu (pokud existuje)
                if (use3DTubes && i < finalPath.Count - 1) {
                    Vector3 nextPos = finalPath[i + 1];
                    CreateTubeSegment(currentPos, nextPos);
                }
            }
        }
    }

    // --- TVORBA WAYPOINTU ---
    private void CreateWaypointMarker(Vector3 pos, int index) {
        GameObject wpObj;

        // Pokud máme Prefab, použijeme ho
        if (waypointPrefab != null) {
            wpObj = Instantiate(waypointPrefab, pos, Quaternion.identity);
        } else {
            // Jinak vytvoříme primitivní kouli
            wpObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // Obarvíme ji
            var renderer = wpObj.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader)
                shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.color = Color.yellow; // Defaultní barva pokud není prefab
            renderer.material = mat;
        }

        wpObj.name = $"WP_{index}";
        wpObj.transform.position = pos;
        wpObj.transform.localScale = Vector3.one * waypointSize;

        // Nastavení vrstvy
        int layerIndex = LayerMask.NameToLayer("Buildings");
        wpObj.layer = (layerIndex != -1) ? layerIndex : 0;

        spawnedObjects.Add(wpObj);
    }

    // --- TVORBA TRUBKY (VÁLCE) ---
    private void CreateTubeSegment(Vector3 start, Vector3 end) {
        GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tube.name = "Tube_Segment";

        // Vrstva
        int layerIndex = LayerMask.NameToLayer("Buildings");
        tube.layer = (layerIndex != -1) ? layerIndex : 0;

        // Materiál trubky
        var renderer = tube.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader)
            shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = pathColor;
        // Můžeme přidat trochu lesku
        mat.SetFloat("_Smoothness", 0.5f);
        renderer.material = mat;

        // MATEMATIKA: Napozicovat válec mezi dva body
        Vector3 centerPos = (start + end) / 2f;
        float distance = Vector3.Distance(start, end);

        tube.transform.position = centerPos;
        tube.transform.LookAt(end);

        // Otočit, protože Unity Cylinder má výšku na ose Y, ale LookAt míří osou Z
        tube.transform.Rotate(90, 0, 0);

        // Změnit velikost (Scale)
        // Y je výška válce (defaultně 2 metry), takže musíme dělit 2
        tube.transform.localScale = new Vector3(tubeThickness, distance / 2f, tubeThickness);

        spawnedObjects.Add(tube);
    }

    public void ClearPath() {
        lineRenderer.positionCount = 0;

        // Smažeme všechny vygenerované objekty
        foreach (var obj in spawnedObjects) {
            if (obj != null)
                Destroy(obj);
        }
        spawnedObjects.Clear();
    }
}
