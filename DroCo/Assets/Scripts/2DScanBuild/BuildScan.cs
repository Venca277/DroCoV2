using System.Collections.Generic;
using UnityEngine;
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

    private List<GameObject> waypointInstances = new List<GameObject>();

    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            Ray ray = arcgisCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f)) {
                Vector3 center = hit.point;
                GenerateCircularMission(center);
            }
        }
    }

    void GenerateCircularMission(Vector3 center) {
        foreach (var wp in waypointInstances)
            Destroy(wp);
        waypointInstances.Clear();


        LineRenderer line = new GameObject("WaypointLine").AddComponent<LineRenderer>();
        line.transform.parent = missionParent;
        line.material = material;
        line.startWidth = 0.2f;
        line.endWidth = 0.2f;
        line.positionCount = numPoints + 1;
        line.loop = true;


        float angleStep = 360f / numPoints;
        for (int i = 0; i < numPoints; i++) {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float dx = Mathf.Cos(angle) * radiusMeters;
            float dz = Mathf.Sin(angle) * radiusMeters;


            Vector3 wp = new Vector3(center.x + dx,center.y + flightHeight,center.z + dz);

            if (waypointPrefab) {
                GameObject instance = Instantiate(waypointPrefab, wp, Quaternion.identity, missionParent);
                Renderer r = instance.GetComponent<Renderer>();
                if (r)
                    r.material.color = Color.red;
                waypointInstances.Add(instance);
            }


            line.SetPosition(i, wp);
        }


        // Uzavřeme smyčku
        line.SetPosition(numPoints, line.GetPosition(0));


        Debug.Log("Generated " + numPoints + " red waypoints and connected them with a green line.");
    }
}
