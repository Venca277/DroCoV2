using System.Collections;
using System.Collections.Generic;
using Esri.ArcGISMapsSDK.Components;
using UnityEngine;

public class DroneManager : Singleton<DroneManager> {

    [SerializeField]
    private bool UseBuffer = false;

    public IDictionary<string, Drone> Drones = new Dictionary<string, Drone>();

    public GameObject DronePrefab;
    public Transform Scene3DView;


    public void HandleReceivedDroneList(DroneStaticData[] droneStaticDatas) {
        List<string> dronesToKeep = new List<string>();
        List<string> dronesToRemove = new List<string>();

        foreach (DroneStaticData dsd in droneStaticDatas) {
            if (Drones.ContainsKey(dsd.client_id)) {
                Drones[dsd.client_id].StaticData = dsd;
            } else {
                AddDrone(dsd);
            }

            dronesToKeep.Add(dsd.client_id);
        }

        foreach (KeyValuePair<string, Drone> drone in Drones) {
            if (!dronesToKeep.Contains(drone.Key)) {
                dronesToRemove.Add(drone.Key);
            }
        }

        foreach (string droneId in dronesToRemove) {
            Drones.TryGetValue(droneId, out Drone droneToBeRemoved);
            Drones.Remove(droneId);
            Destroy(droneToBeRemoved.gameObject);
        }
    }

    public void HandleReceivedDroneData(DroneFlightData flightData) {
        if (Drones.ContainsKey(flightData.client_id)) {
            GameManager.Instance.CenterMap(flightData);
            if (UseBuffer) {
                Drones[flightData.client_id].DeliverNewFlightData(flightData);
            } else {
                Drones[flightData.client_id].UpdateDroneFlightData(flightData);
            }
        } else { //prisla data s neznamym drone ID -> pozadame server o novy seznam dronu
            if (GameManager.Instance.CurrentAppMode == GameManager.AppMode.Client) {
                WebSocketClient.Instance.SendDroneListRequest();
            }
        }
    }

    public void HandleReceivedVehicleData(DroneVehicleData vehicleData) {
        if (Drones.ContainsKey(vehicleData.client_id)) {
            Drones[vehicleData.client_id].UpdateDroneVehicleData(vehicleData);
        } else { //prisla data s neznamym drone ID -> pozadame server o novy seznam dronu
            if (GameManager.Instance.CurrentAppMode == GameManager.AppMode.Client) {
                WebSocketClient.Instance.SendDroneListRequest();
            }
        }
    }

    public void AddDrone(DroneStaticData dsd) {
        Debug.Log("adding new drone with id: " + dsd.client_id);

        GameObject newDroneGameObj = Instantiate(DronePrefab, Scene3DView);
        Drone newDrone = newDroneGameObj.GetComponent<Drone>();
        newDrone.InitDrone(dsd);
        Drones.Add(dsd.client_id, newDrone);

        // Init drone's UI
        newDrone.DroneListItem = UIManager.Instance.MainScreen.UnitList.SpawnListItemDrone(dsd, newDrone);
        RenderTexture renderTexture = new RenderTexture(1280, 720, 24);
        newDrone.TPVCamera.targetTexture = renderTexture;
        newDrone.DroneListItem.InitCameraViewTexture(renderTexture);
    }

    public void SetDroneModelsActive(bool active) {
        foreach (KeyValuePair<string, Drone> drone in Drones) {
            drone.Value.DroneModel.gameObject.SetActive(active);
        }
    }

    public void DestroyDroneAll() {
        foreach (KeyValuePair<string, Drone> drone in Drones) {
            DestroyDrone(drone.Value);
        }
        Drones.Clear();
    }

    public void DestroyDrone(Drone drone) {
        Destroy(drone.gameObject);
    }
}
