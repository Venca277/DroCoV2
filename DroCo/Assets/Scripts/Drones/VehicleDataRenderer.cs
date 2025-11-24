using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class VehicleDataRenderer : MonoBehaviour
{
    private Material material;
    private Texture2D vehicleTexture;

    private DroneVehicleData vehicleData;

    private void Awake() {
        material = GetComponent<MeshRenderer>().material;
        vehicleTexture = new Texture2D(1280, 720, TextureFormat.ARGB32, false);
        ClearTexture();

        material.mainTexture = vehicleTexture;
    }

    public void UpdateVehicleData(DroneVehicleData data) {
        vehicleData = data;

        ClearTexture();

        foreach (MyRect rect in data.rects) {
            DrawRectangle(rect);
        }
    }

    private void DrawRectangle(MyRect rect) {
        //for (int y = rect.y; y < rect.y + rect.h; y++) {
        //    for (int x = rect.x; x < rect.x + rect.w; x++) {
        //        vehicleTexture.SetPixel(x, y, Color.green);
        //    }
        //}
        vehicleTexture = Box(vehicleTexture, new Vector2(rect.x, vehicleTexture.height - rect.y - 150), new Vector2(rect.x + rect.w, vehicleTexture.height - rect.y + rect.h - 150), Color.green);
        vehicleTexture.Apply();
    }

    private void ClearTexture() {
        Color fillColor = Color.clear;
        Color[] fillPixels = new Color[vehicleTexture.width * vehicleTexture.height];

        for (int i = 0; i < fillPixels.Length; i++) {
            fillPixels[i] = fillColor;
        }

        vehicleTexture.SetPixels(fillPixels);
        vehicleTexture.Apply();
    }


    // Draw a box given two vec2d points.
    // Top left corner, bottom right corner
    public Texture2D Box(Texture2D tex, Vector2 tl, Vector2 br, Color color) {
        // Draw line connecting top left and top right
        var tr = new Vector2(br.x, tl.y);
        tex = Line(tex, tl, tr, color);

        // Draw line connecting top left and bottom left
        var bl = new Vector2(tl.x, br.y);
        tex = Line(tex, tl, bl, color);

        // Draw line connecting bottom left and bottom right
        tex = Line(tex, bl, br, color);

        // Draw line connecting bottom right and top right
        tex = Line(tex, br, tr, color);

        return tex;
    }


    private Texture2D Line(Texture2D tex, Vector2 p1, Vector2 p2, Color color) {
        Vector2 t = p1;
        float frac = 1 / Mathf.Sqrt(Mathf.Pow(p2.x - p1.x, 2) + Mathf.Pow(p2.y - p1.y, 2));
        float ctr = 0;

        while ((int) t.x != (int) p2.x || (int) t.y != (int) p2.y) {
            t = Vector2.Lerp(p1, p2, ctr);
            ctr += frac;
            tex.SetPixel((int) t.x - 2, (int) t.y - 2, color);
            tex.SetPixel((int) t.x - 1, (int) t.y - 1, color);
            tex.SetPixel((int) t.x, (int) t.y, color);
            tex.SetPixel((int) t.x + 1, (int) t.y + 1, color);
            tex.SetPixel((int) t.x + 2, (int) t.y + 2, color);
        }
        return tex;
    }


}
