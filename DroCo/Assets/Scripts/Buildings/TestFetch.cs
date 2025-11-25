using UnityEngine;

public class OverpassTest : MonoBehaviour {
    private OverpassClient client;

    void Start() {
        client = GetComponent<OverpassClient>();
        if (client != null) {
            Debug.Log("OverpassClient found on same GameObject.");
        } else {
            client = FindObjectOfType<OverpassClient>();
            if (client != null) {
                Debug.Log("OverpassClient found in scene via FindObjectOfType.");
            } else {
                Debug.LogError("OverpassClient NOT FOUND in scene. Attach OverpassClient to a GameObject.");
            }
        }

        Debug.Log("Press T to test fetching OSM data from Overpass API.");
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.T)) {
            Debug.Log("Key T pressed");

            if (client == null) {
                client = GetComponent<OverpassClient>() ?? FindObjectOfType<OverpassClient>();
                if (client == null) {
                    Debug.LogError("OverpassClient still not found. Please attach OverpassClient component to a GameObject in the scene.");
                    return;
                } else {
                    Debug.Log("OverpassClient found on retry.");
                }
            }

            Debug.Log("Starting Overpass");
            //test fetch on FIT VUT D105
            StartCoroutine(client.FetchBuildingData(
                49.2262223892531,    // LAT
                16.5969122730065,    // LON
                (json) => {
                    if (string.IsNullOrEmpty(json)) {
                        Debug.LogError("OVERPASS FAILED! (Callback received null/empty)");
                    } else {
                        Debug.Log("OVERPASS SUCCESS — RAW JSON BELOW:");
                        Debug.Log(json);
                        Debug.Log($"JSON length: {json.Length} characters");
                    }
                }
            ));
        }
    }
}
