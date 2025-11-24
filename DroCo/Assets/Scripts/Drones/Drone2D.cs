using System.Collections;
using System.Collections.Generic;
using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Geometry;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ArcGISLocationComponent))]
public class Drone2D : MonoBehaviour {

    [SerializeField]
    private Image droneImage;
    private ArcGISLocationComponent GPSLocation;

    private Camera Map2DCamera;
    [SerializeField]
    private float FixedSize = 0.005f;


    private void Update() {
        if (Map2DCamera != null) {
            AdaptSize();
        }
    }

    public void InitDrone() {
        GPSLocation = GetComponent<ArcGISLocationComponent>();
        GPSLocation.enabled = true;
        Map2DCamera = GameManager.Instance.MinimapCamera.GetComponent<Camera>();
    }

    public void UpdateFlightData(DroneFlightData flightData) {
        GPSLocation.Position = new ArcGISPoint(flightData.gps.longitude, flightData.gps.latitude, 0, new ArcGISSpatialReference(4326));
        GPSLocation.Rotation = new ArcGISRotation(flightData.aircraft_orientation.yaw, 0, 0);
    }

    public void Highlight(bool highlight) {
        droneImage.color = highlight ? Color.green : Color.black;
    }

    private void AdaptSize() {
        float distance = (Map2DCamera.transform.position - transform.position).magnitude;
        float size = distance * FixedSize * Map2DCamera.fieldOfView;
        droneImage.transform.localScale = Vector3.one * size;
        //droneImage.transform.forward = droneImage.transform.position - Map2DCamera.transform.position;
    }
}
