using System;
using System.Collections.Generic;

[Serializable]
public class OSMRoot {
    public List<OSMElement> elements;
}

[Serializable]
public class OSMElement {
    public string type;
    public long id;
    public List<OSMCoord> geometry;
}

[Serializable]
public class OSMCoord {
    public double lat;
    public double lon;
}
