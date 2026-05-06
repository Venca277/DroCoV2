// ============================================================
// DroneDataPoller.cs
//
// Author:  Václav Sovák
// Date:    2026-04-05
//
// Incoming flight data is pushed from a background thread and 
// processed on the main thread.
// ============================================================

using UnityEngine;

public class DroneDataPoller : MonoBehaviour {
    public static DroneDataPoller Instance;

    private DroneFlightData latestFlightData = null;
    private object dataLock = new object();

    private void Awake() {
        Instance = this;
    }

    //store latest data
    public void PushLatestData(DroneFlightData data) {
        lock (dataLock) {
            latestFlightData = data;
        }
    }

    private void Update() {
        DroneFlightData toProcess = null;

        //clear the latest data on main thread
        lock (dataLock) {
            if (latestFlightData != null) {
                toProcess = latestFlightData;
                latestFlightData = null;
            }
        }

        //process the data
        if (toProcess != null) {
            GameManager.Instance.HandleReceivedDroneData(toProcess);
        }
    }
}