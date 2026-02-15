using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AdjustLayout : MonoBehaviour {
    void Start() {
        StartCoroutine(FixLayout());
    }

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
