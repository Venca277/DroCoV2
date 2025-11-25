using System.Collections.Generic;
using UnityEngine;

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
