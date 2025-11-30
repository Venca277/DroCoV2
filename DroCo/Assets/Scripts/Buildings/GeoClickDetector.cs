using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Geometry;
using UnityEngine;
using Unity.Mathematics;

public class TestGeo : MonoBehaviour {
    public Camera arcgisCamera;
    private ArcGISMapComponent mapComponent;

    void Start() {
        mapComponent = GetComponentInParent<ArcGISMapComponent>();
        if (mapComponent == null)
            mapComponent = FindObjectOfType<ArcGISMapComponent>();

        if (mapComponent == null) {
            Debug.LogError("ArcGISMapComponent not found!");
        }

        if (arcgisCamera == null) {
            arcgisCamera = Camera.main;
            if (arcgisCamera == null)
                Debug.LogWarning("arcgisCamera not assigned and Camera.main");
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

                ArcGISPoint geo = mapComponent.EngineToGeographic(hit.point);

                Debug.Log($"Lon: {geo.X}, Lat: {geo.Y}, Alt: {geo.Z}");
            }
        }
    }
}
