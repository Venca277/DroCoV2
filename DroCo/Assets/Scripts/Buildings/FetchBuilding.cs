// ============================================================
// FetchBuilding.cs
//
// Author:  Václav Sovák
// Date:    2026-04-05
//
// Request script for OSM Overpass API. Fetches building 
// footprint data around given GPS coordinates.
// Retries up to 3 times on failure with a delay between attempts.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Globalization;

public class OverpassClient : MonoBehaviour {
    //fetches building data from Overpass API around given lat, lon
    public IEnumerator FetchBuildingData(double lat, double lon, System.Action<string> callback, int radius = 80) {

        //formating lat and long to contain allowed chars like . not ,
        string latStr = lat.ToString(CultureInfo.InvariantCulture);
        string lonStr = lon.ToString(CultureInfo.InvariantCulture);

        //larger radius needs more time to precess
        int timeout = 20;
        if (radius > 100)
            timeout = 40;

        //overpass api query
        string query =
            $"[out:json][timeout:{timeout}];" +
            $"(way[\"building\"](around:{radius},{latStr},{lonStr}););" +
            $"out geom;";

        //form for maintaning compatibility with desired request type
        WWWForm form = new WWWForm();
        form.AddField("data", query);

        int attempts = 3;
        for (int i = 0; i < attempts; i++) {
            using (UnityWebRequest req = UnityWebRequest.Post("https://overpass-api.de/api/interpreter", form)) {
                //user and timetout settings
                req.SetRequestHeader("User-Agent", "UnityOverpassClient/1.0");
                req.timeout = 45;
                if (radius > 100)
                    req.timeout = 65;

                Debug.Log("Sending Overpass request...");
                yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                bool failed = req.result != UnityWebRequest.Result.Success;
#else
                bool failed = req.isNetworkError || req.isHttpError;
#endif
                if (!failed) {
                    string text = req.downloadHandler.text;
                    if (!string.IsNullOrEmpty(text)) {
                        Debug.Log("Overpass success. Response length: " + text.Length);
                        callback?.Invoke(text);
                        yield break;    //success break from retrying
                    }
                }

                Debug.LogWarning($"Overpass attempt {i + 1} failed: {req.error}");
                if (i < attempts - 1) {
                    yield return new WaitForSeconds(4f); //wait before retrying
                } else {
                    Debug.LogError("Overpass failed all attempts.\n" + (req.downloadHandler != null ? req.downloadHandler.text : ""));
                    Toast.call.Show("OSM request failed", 2f, true);
                    callback?.Invoke(null);
                }
            }
        }
    }

}


