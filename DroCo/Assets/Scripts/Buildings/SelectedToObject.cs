using System.Collections.Generic;
using UnityEngine;
using Esri.GameEngine.Geometry;
using Esri.ArcGISMapsSDK.Components;

public static class OSMToUnity {
    public static List<Vector3> ConvertPolygonToUnity(OSMElement building, ArcGISMapComponent map, double height = 0) {
        var result = new List<Vector3>();

        if (building == null || building.geometry == null || map == null)
            return result;

        foreach (var p in building.geometry) {
            // OSM: p.lon = longitude (X), p.lat = latitude (Y)
            // Použijeme správnou konstrukci spatial reference a map.GeographicToEngine která vrací Unity Vector3
            var arcPoint = new ArcGISPoint(p.lon, p.lat, height, ArcGISSpatialReference.WGS84());
            Vector3 world = map.GeographicToEngine(arcPoint);

            result.Add(world);
        }

        return result;
    }
}
