using System;
using System.Collections.Generic;
using UnityEngine;
using Esri.GameEngine.Geometry;
using Esri.ArcGISMapsSDK.Components;

public class OSMRoot {
    public List<OSMElement> elements;
}

public class OSMElement {
    public string type;
    public long id;
    public List<OSMCoord> geometry;
    public Dictionary<string, string> tags;
}

public class OSMCoord {
    public double lat;
    public double lon;
}

public static class OSMBuildingSelector {
    public static OSMElement FindClosestBuilding(OSMRoot root, double clickLat, double clickLon) {
        OSMElement best = null;
        double bestDist = double.MaxValue;

        foreach (var e in root.elements) {
            if (e.type != "way" || e.geometry == null || e.geometry.Count == 0)
                continue;

            double sumLat = 0;
            double sumLon = 0;

            foreach (var p in e.geometry) {
                sumLat += p.lat;
                sumLon += p.lon;
            }

            double cLat = sumLat / e.geometry.Count;
            double cLon = sumLon / e.geometry.Count;

            double dLat = cLat - clickLat;
            double dLon = cLon - clickLon;
            double distSq = dLat * dLat + dLon * dLon;

            if (distSq < bestDist) {
                bestDist = distSq;
                best = e;
            }
        }

        return best;
    }
}

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