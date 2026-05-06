// ============================================================
// PanelHider.cs
//
// Author: Václav Sovák
// Date: 2026-05-05
//
// Toggles a panel open/close via Animator
// and swaps the button icon accordingly.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelHider : MonoBehaviour {
    public Animator animator;
    public Image panel;
    public Sprite panelOpen;
    public Sprite panelClose;

    private bool isOpen = false;

    public void TogglePanel() {
        isOpen = !isOpen;
        if (isOpen) {
            panel.sprite = panelClose;
            animator.SetBool("isOpen", true);
        } else {
            panel.sprite = panelOpen;
            animator.SetBool("isOpen", false);
        }
    }
}
