// ============================================================
// AdjustLayout.cs
//
// Author: Václav Sovák
// Date: 2026-05-05
//
// Forces a layout rebuild on Start and
// OnEnable. Unity layout system doesn't update nested components.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AdjustLayout : MonoBehaviour {
    void Start() {
        StartCoroutine(FixLayout());
    }

    //force layout rebuild on parent
    //second rebuild settles depending children
    IEnumerator FixLayout() {
        yield return new WaitForEndOfFrame();

        RectTransform rectTransform = GetComponent<RectTransform>();

        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }

    void OnEnable() {
        StartCoroutine(FixLayout());
    }
}
