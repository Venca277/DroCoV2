// ============================================================
// BuildingTag.cs
//
// Author: Václav Sovák
// Date: 2026-05-06
//
// Marker component attached to each preloaded building object.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
public class BuildingTag : MonoBehaviour {
    public OSMElement BuildingData;     //OSM data of the building
    public List<Vector3> WorldPoints;   //world points of the building footprint
}