using UnityEngine;
using UnityEditor;

public class FindMissingScripts {
    [MenuItem("Tools/Find Missing Scripts in Scene")]
    public static void FindMissing() {
        var gos = GameObject.FindObjectsOfType<GameObject>();
        int count = 0;
        foreach (var go in gos) {
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++) {
                if (comps[i] == null) {
                    Debug.LogWarning($"Missing script on: {GetPath(go)}", go);
                    count++;
                }
            }
        }
        Debug.Log($"FindMissing finished — found {count} missing components.");
    }

    static string GetPath(GameObject go) {
        string path = go.name;
        var t = go.transform;
        while (t.parent != null) {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}

