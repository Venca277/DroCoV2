using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SimpleAccordion : MonoBehaviour {
    [Header("Content")]
    public GameObject contentObject;

    public void Toggle() {
        bool currentState = contentObject.activeSelf;
        contentObject.SetActive(!currentState);

        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);

        if (transform.parent != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
        }
    }
}
