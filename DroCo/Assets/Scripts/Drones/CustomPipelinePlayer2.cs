using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(GstCustomTexture))]
public class CustomPipelinePlayer2 : BaseVideoPlayer {

    public string pipeline = "";

    public DroneVehicleData VehicleData {
        get; set;
    }

    // Use this for initialization
    protected override string _GetPipeline() {
        string P = pipeline + " ! video/x-raw,format=I420 ! videoconvert ! appsink name=videoSink";

        return P;
    }

    protected override void Update() {
        base.Update();
        //Debug.Log("texture " + InternalTexture.PlayerTexture()[0].width + ":" + InternalTexture.PlayerTexture()[0].height);
        if (VehicleData != null) {
            foreach (MyRect r in VehicleData.rects) {
                for (int y = r.y; y < r.y + r.h; y++) {
                    for (int x = r.x; x < r.x + r.w; x++) {
                        InternalTexture.PlayerTexture()[0].SetPixel(x, y, Color.green);
                    }
                }
                //InternalTexture.PlayerTexture()[0].SetPixels(r.x, r.y, r.w, r.h, new Color[] { Color.green });
                //Graphics.DrawTexture(new Rect(r.x, r.y, r.w, r.h), VideoTexture);
            }
        }
    }
}
