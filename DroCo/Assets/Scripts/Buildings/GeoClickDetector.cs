using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Geometry;
using UnityEngine;
using Unity.Mathematics;

public class TestGeo : MonoBehaviour {
    public Camera arcgisCamera;
    private ArcGISMapComponent mapComponent;

    void Start() {
        // Najdi ArcGISMapComponent v parent objektech nebo jako fallback v celé scéně
        mapComponent = GetComponentInParent<ArcGISMapComponent>();
        if (mapComponent == null)
            mapComponent = FindObjectOfType<ArcGISMapComponent>();

        if (mapComponent == null) {
            Debug.LogError("ArcGISMapComponent not found! Make sure this script is a child of the map object or that an ArcGISMapComponent exists in the scene.");
        }

        if (arcgisCamera == null) {
            arcgisCamera = Camera.main;
            if (arcgisCamera == null)
                Debug.LogWarning("arcgisCamera not assigned and Camera.main is null. Assign the ArcGIS camera in the Inspector.");
        }
    }

    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            Ray ray = arcgisCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 500f)) {
                if (mapComponent == null) {
                    Debug.LogWarning("Map component not ready.");
                    return;
                }

                // Správné volání: použijeme EngineToGeographic, které interně bere v úvahu WorldMatrix mapy.
                ArcGISPoint geo = mapComponent.EngineToGeographic(hit.point);

                Debug.Log($"Lon: {geo.X}, Lat: {geo.Y}, Alt: {geo.Z}");
            }
        }
    }
}
