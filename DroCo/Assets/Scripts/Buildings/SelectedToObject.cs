using System.Collections.Generic;
using UnityEngine;
using Esri.GameEngine.Geometry;
using Esri.ArcGISMapsSDK.Components;
using Unity.Mathematics;

public static class OSMToUnity {
    public static List<Vector3> ConvertPolygonToUnity(OSMElement building, ArcGISMapComponent map, double baseAltitude = 0, double heightOffset = 0) {
        var result = new List<Vector3>();

        if (building == null || building.geometry == null || map == null)
            return result;

        foreach (var p in building.geometry) {
            var finalGeo = new ArcGISPoint(p.lon, p.lat, baseAltitude + heightOffset, ArcGISSpatialReference.WGS84());
            Vector3 world = map.GeographicToEngine(finalGeo);
            result.Add(world);
        }

        return result;
    }
}

