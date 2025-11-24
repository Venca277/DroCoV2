using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SetGStreamerPath : MonoBehaviour {

    private void Start() {
        SetGStreamer();
    }

    private void SetGStreamer() {
        string path = Environment.GetEnvironmentVariable("Path");
        path = path + ";" + Application.dataPath + "/gstreamer/1.0/msvc_x86_64/bin/";
        Environment.SetEnvironmentVariable("Path", path);

        SceneManager.LoadScene(1);
    }

}
