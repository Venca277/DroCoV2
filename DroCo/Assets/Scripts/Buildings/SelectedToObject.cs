using System.Collections.Generic;
using UnityEngine;
using Esri.GameEngine.Geometry;
using Esri.ArcGISMapsSDK.Components;
using Unity.Mathematics;

public static class OSMToUnity {
    public static List<Vector3> ConvertPolygonToUnity(OSMElement building, ArcGISMapComponent map, double baseAltitude) {
        var result = new List<Vector3>();
        foreach (var p in building.geometry) {
            //use base altitude for all points
            var finalGeo = new ArcGISPoint(p.lon, p.lat, baseAltitude, ArcGISSpatialReference.WGS84());
            result.Add(map.GeographicToEngine(finalGeo));
        }
        return result;
    }
}

