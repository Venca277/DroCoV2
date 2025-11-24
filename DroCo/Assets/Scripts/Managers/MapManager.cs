using System.Collections;
using System.Collections.Generic;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Map;
using UnityEngine;

public class MapManager : Singleton<MapManager> {

    public enum Location {
        PRAGUE,
        BRNO,
        PILSEN,
        UNKNOWN
    }

    public ArcGISMapComponent ArcGISMap;
    public Location DefaultLocation;

    private MapArea currentArea;

    private List<MapArea> mapAreas;

    private void Start() {
        mapAreas = new List<MapArea> {
            new BrnoData(),
            new PilsenData()
        };
    }

    public Location GetKnownLocation(GPS gpsPosition) {
        foreach (MapArea area in mapAreas) {
            if (area.IsPointInsideArea(gpsPosition)) {
                currentArea = area;
                return area.GetLocation();
            }
        }
        return Location.UNKNOWN;
    }

    public string[] Get3DObjectSceneLayerData() {
        return currentArea == null ? new string[] { } : currentArea.Get3DObjectSceneLayerData();
    }

    public string[] GetElevationData() {
        return currentArea == null ? new string[] { } : currentArea.GetElevationData();
    }

}

public class PragueData : MapArea {
    public override string[] Get3DObjectSceneLayerData() {
        throw new System.NotImplementedException();
    }

    public override string[] GetElevationData() {
        throw new System.NotImplementedException();
    }

    public override MapManager.Location GetLocation() {
        throw new System.NotImplementedException();
    }

    public override void InitArea() {
        throw new System.NotImplementedException();
    }
}

public class BrnoData : MapArea {
    public static string BrnoElevation = "https://gis.brno.cz/ags1/rest/services/OMI/OMI_DMT_2019_wgs_elevation/ImageServer";
    public static string BrnoCompleteLOD2 = "https://gis.brno.cz/ags1/rest/services/OMI/OMI_3D_budovy_lod2_wgs/SceneServer";
    //public static string BrnoLOD1 = "https://gis.brno.cz/ags1/rest/services/Hosted/Brno_3D_LOD1_SJTSK/SceneServer";
    //public static string BrnoLOD2 = "https://gis.brno.cz/ags1/rest/services/Hosted/3D_budovy_LOD2_WM/SceneServer";


    public BrnoData() {
        InitArea();
    }

    public override string[] Get3DObjectSceneLayerData() {
        return new string[] { BrnoCompleteLOD2 };
    }

    public override string[] GetElevationData() {
        return new string[] { BrnoElevation };
    }

    public override MapManager.Location GetLocation() {
        return MapManager.Location.BRNO;
    }

    public override void InitArea() {
        Area = new Vector2[] {
            new Vector2(49.2635479f, 16.4276505f),
            new Vector2(49.3201989f, 16.6624832f),
            new Vector2(49.1345533f, 16.7644501f),
            new Vector2(49.0819618f, 16.5268707f)
        };
    }
}

public class PilsenData : MapArea {
    public static string Pilsen1 = "https://tiles.arcgis.com/tiles/NUPWFrOotHlwVelG/arcgis/rest/services/3D_budovy_etapa_I_1/SceneServer";
    public static string Pilsen2 = "https://tiles.arcgis.com/tiles/NUPWFrOotHlwVelG/arcgis/rest/services/3D_budovy_etapa_I_2/SceneServer";
    public static string Pilsen3 = "https://tiles.arcgis.com/tiles/NUPWFrOotHlwVelG/arcgis/rest/services/3D_budovy_etapa_II/SceneServer";

    public PilsenData() {
        InitArea();
    }

    public override string[] Get3DObjectSceneLayerData() {
        return new string[] { Pilsen1, Pilsen2, Pilsen3 };
    }

    public override string[] GetElevationData() {
        return new string[] { };
    }

    public override MapManager.Location GetLocation() {
        return MapManager.Location.PILSEN;
    }

    public override void InitArea() {
        Area = new Vector2[] { };
    }
}

public class OpenStreetMap : MapArea {
    public static string OSM3D = "https://basemaps3d.arcgis.com/arcgis/rest/services/OpenStreetMap3D_Buildings_v1/SceneServer";

    public override string[] Get3DObjectSceneLayerData() {
        throw new System.NotImplementedException();
    }

    public override string[] GetElevationData() {
        throw new System.NotImplementedException();
    }

    public override MapManager.Location GetLocation() {
        throw new System.NotImplementedException();
    }

    public override void InitArea() {
        throw new System.NotImplementedException();
    }
}
