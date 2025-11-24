using System.Collections;
using System.Collections.Generic;
using Esri.ArcGISMapsSDK.Components;
using UnityEngine;

public class ChangeLayerAtRuntime : MonoBehaviour {

    [SerializeField, Layer]
    private int Layer;

    private void Start() {
        ArcGISRendererComponent renderer = GetComponentInChildren<ArcGISRendererComponent>(includeInactive:true);
        renderer.gameObject.layer = Layer;
        foreach (Transform child in renderer.GetComponentsInChildren<Transform>(includeInactive:true)) {
            child.gameObject.layer = Layer;
        }
    }
}
