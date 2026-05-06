// ============================================================
// HeighFitterUI.cs
//
// Author: Václav Sovák
// Date: 2026-05-05
//
// Clamps a scroll-view to a max height. Panel grows, 
// but never exceeds maxHeight.
// ============================================================

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

    //read the content height and clamp to maxHeight
    void LateUpdate() {
        if (content != null && layoutElement != null) {
            float h = LayoutUtility.GetPreferredHeight(content);
            layoutElement.preferredHeight = Mathf.Min(h, maxHeight);
        }
    }
}
