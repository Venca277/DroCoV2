using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeighFitterUI : MonoBehaviour {
    public RectTransform content;
    public float maxHeight = 250f;

    private LayoutElement layoutElement;

    void Awake() {
        layoutElement = GetComponent<LayoutElement>();
    }

    void LateUpdate() {
        if (content != null && layoutElement != null) {
            float h = LayoutUtility.GetPreferredHeight(content);
            layoutElement.preferredHeight = Mathf.Min(h, maxHeight);
        }
    }
}
