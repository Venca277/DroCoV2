// ============================================================
// WaypointUI.cs
//
// Author: Václav Sovák
// Date: 2026-05-06
//
// Foldout row in the waypoint list. Expands or
// collapses the detail content.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WaypointUI : MonoBehaviour {

    [Header("UI")]
    public GameObject content;
    public RectTransform maincontent;
    private bool isExpanded = false;

    void Start() {
        if (content != null)
            content.SetActive(isExpanded);

        Button btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(ToggleFoldout);
    }

    public void ToggleFoldout() {
        isExpanded = !isExpanded;
        content.SetActive(isExpanded);

        if (gameObject.activeInHierarchy)
            StartCoroutine(updateAfterClose());
    }

    //updates the layout after closing the foldout
    IEnumerator updateAfterClose() {
        yield return new WaitForEndOfFrame();

        if (maincontent != null) {
            Canvas.ForceUpdateCanvases();

            //rerender content
            LayoutRebuilder.ForceRebuildLayoutImmediate(maincontent);

            //rerender missionlist
            if (maincontent.parent != null) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(maincontent.parent.GetComponent<RectTransform>());

                //rerender sidebar
                if (maincontent.parent.parent != null) {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(maincontent.parent.parent.GetComponent<RectTransform>());
                }
            }
        }
    }
}

