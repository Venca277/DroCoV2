using System;
using System.Collections;
using System.Collections.Generic;
using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Elevation.Base;
using Esri.GameEngine.Geometry;
using UnityEngine;

public class Drone : InteractiveObject, IPointerNotifier {

    public DroneFlightData FlightData {
        get; set;
    }

    public DroneStaticData StaticData {
        get; set;
    }

    public VehicleDataRenderer VehicleRenderer;
    //public CustomPipelinePlayer StreamPlayer;
    //public Material VideoMaterial;
    //public MeshRenderer VideoScreen;
    public MeshRenderer VideoScreen;

    private ArcGISLocationComponent GPSLocation;

    public Transform DroneVideoScreen;
    public Transform ThirdPersonView;

    public ListItemButton DroneListItem;

    public GameObject Drone2DPrefab;
    private Drone2D drone2DRepresentation;

    private Queue<DroneFlightData> flightDataBuffer = new Queue<DroneFlightData>();
    private float flightDataDelay = 0f;
    public int FlightDataBufferSize = 0;
    private float dataFrameRate = 0.1f;
    private float lastMessageReceiveTime = 0f;

    private bool decoded = false;

    private Texture2D JpegPlayerTexture;

    public Material OcclusionMaterial;


    private IEnumerator PlayReceivedFlightData() {
        while (true) {
            if (flightDataBuffer.Count > 0) {
                DroneFlightData flightData = flightDataBuffer.Dequeue();
                UpdateDroneFlightData(flightData);
                yield return new WaitForSeconds(dataFrameRate);
            } else {
                yield return new WaitForEndOfFrame();
            }

            yield return new WaitForSeconds(flightDataDelay);
            flightDataDelay = 0f;
        }
    }

    public void InitDrone(DroneStaticData staticData) {
        GPSLocation = GetComponent<ArcGISLocationComponent>();
        GPSLocation.enabled = true;

        drone2DRepresentation = Instantiate(Drone2DPrefab, GameManager.Instance.Map2DViewArcGISMap.transform).GetComponent<Drone2D>();
        drone2DRepresentation.InitDrone();

        MissionManager.Instance.ChangeTargetFaceCamera(FPVOnlyCamera.transform);
        MissionManager.Instance.ChangeTarget(transform);

        //Material mat = new Material(VideoMaterial);
        //VideoScreen.material = mat;
        //StreamPlayer = new CustomPipelinePlayer2();
        //StreamPlayer = AddComponent()
        //Instantiate(StreamPlayer);

        //StartCoroutine(PlayReceivedFlightData());

        JpegPlayerTexture = new Texture2D(1, 1);
    }

    public void DeliverNewFlightData(DroneFlightData flightData) {
        dataFrameRate = Time.time - lastMessageReceiveTime;
        lastMessageReceiveTime = Time.time;
        Debug.Log(dataFrameRate);
        flightDataBuffer.Enqueue(flightData);
        FlightDataBufferSize = flightDataBuffer.Count;
    }

    public void UpdateDroneFlightData(DroneFlightData flightData) {
        FlightData = flightData;

        GPSLocation.Position = new ArcGISPoint(flightData.gps.longitude, flightData.gps.latitude, flightData.altitude, new ArcGISSpatialReference(4326));
        DroneModel.localRotation = Quaternion.Euler(-(float) flightData.aircraft_orientation.pitch, (float) flightData.aircraft_orientation.yaw, -(float) flightData.aircraft_orientation.roll);
        //DroneVideoScreen.localRotation = Quaternion.Euler(-(float) flightData.aircraft_orientation.pitch, (float) flightData.aircraft_orientation.yaw, -(float) flightData.aircraft_orientation.roll);
        //DroneVideoScreen.localRotation = Quaternion.Euler(-(float) flightData.gimbal_orientation.pitch, (float) flightData.gimbal_orientation.yaw, -(float) flightData.gimbal_orientation.roll);
        DroneVideoScreen.localRotation = Quaternion.Euler(-(float) flightData.gimbal_orientation.pitch, (float) flightData.aircraft_orientation.yaw + (float) flightData.gimbal_orientation.yaw_relative, -(float) flightData.gimbal_orientation.roll);
        ThirdPersonView.localRotation = Quaternion.Euler(0f, (float) flightData.aircraft_orientation.yaw, 0f);
        
        drone2DRepresentation.UpdateFlightData(flightData);

        DroneListItem.UpdateHeight(flightData.altitude);
        DroneListItem.UpdateDistance(Vector3.Distance(Camera.main.transform.position, this.transform.position));

        if (flightData.frame != "") {
            byte[] frame = Convert.FromBase64String(flightData.frame);

            JpegPlayerTexture.LoadImage(frame);

            if (ArCameraBackground != null) {
                ArCameraBackground.texture = JpegPlayerTexture;
            }
            VideoScreen.material.mainTexture = JpegPlayerTexture;
            OcclusionMaterial.SetTexture("_CameraFeedTexture", JpegPlayerTexture);
        }
    }

    public override Texture GetCameraTexture() {
        return JpegPlayerTexture;
    }

    public void UpdateDroneVehicleData(DroneVehicleData vehicleData) {
        //if(PipelinePlayer == null && VehicleRenderer == null) return;
        //VehicleRenderer.gameObject.SetActive(true);
        //VehicleRenderer.UpdateVehicleData(vehicleData);
    }

    public override void Highlight(bool highlight) {
        base.Highlight(highlight);
        drone2DRepresentation.Highlight(highlight);
    }

    public override void ChangeFlightDataDelay(float delay) {
        flightDataDelay = delay;
    }

    public void OnClicked(IPointerNotifier.ClickObject clickObject) {
        if (clickObject == IPointerNotifier.ClickObject.DroneScreen) {
            CameraManager.Instance.SetCurrentInteractiveObject(this);
            CameraManager.Instance.SetCameraView(CameraManager.CameraView.FirstPerson);
        }
    }

    public void OnDestroy() {
        Destroy(DroneListItem.gameObject);
        Destroy(drone2DRepresentation.gameObject);
    }
}
