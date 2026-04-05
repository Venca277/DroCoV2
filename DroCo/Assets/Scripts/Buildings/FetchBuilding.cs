using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Globalization;

public class OverpassClient : MonoBehaviour {
    public IEnumerator FetchBuildingData(double lat, double lon, System.Action<string> callback, int radius = 80) {

        //formating lat and long to contain allowed chars like . not ,
        string latStr = lat.ToString(CultureInfo.InvariantCulture);
        string lonStr = lon.ToString(CultureInfo.InvariantCulture);

        int timeout = 25;
        if (radius > 100)
            timeout = 45;

        //overpass querry
        string query =
            $"[out:json][timeout:{timeout}];" +
            $"(way[\"building\"](around:{radius},{latStr},{lonStr}););" +
            $"out geom;";

        //form for maintaning compatibility with desired request type
        WWWForm form = new WWWForm();
        form.AddField("data", query);

        using (UnityWebRequest req = UnityWebRequest.Post("https://overpass-api.de/api/interpreter", form)) {
            //user and timetout settings
            req.SetRequestHeader("User-Agent", "UnityOverpassClient/1.0");
            req.timeout = 30;
            if (radius > 100)
                req.timeout = 60;

            Debug.Log("Sending Overpass request...");
            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success) {
#else
            if (req.isNetworkError || req.isHttpError) {
#endif
                Debug.LogError("Overpass error: " + req.error);
                Debug.LogError("Server response:\n" + (req.downloadHandler != null ? req.downloadHandler.text : "<no response>"));
                Toast.call.Show("OSM request error", 2f, true);
                callback?.Invoke(null);
                yield break;
            }

            string text = req.downloadHandler.text;
            if (string.IsNullOrEmpty(text)) {
                Debug.LogWarning("Overpass returned empty response.");
                Toast.call.Show("Failed loading building data", 2f, true);
                callback?.Invoke(null);
                yield break;
            }

            Debug.Log("Overpass success. Response length: " + text.Length);
            callback?.Invoke(text);
        }
    }
}


