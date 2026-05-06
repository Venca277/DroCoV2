// ============================================================
// togle_toolpanel.cs
//
// Author: Václav Sovák
// Date: 2026-05-06
//
// Toggles a panel open/close via Animator bool.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class togle_toolpanel : MonoBehaviour {
    public Animator animator;

    public void TogglePanel() {
        bool isOpen = animator.GetBool("isOpen");
        animator.SetBool("isOpen", !isOpen);
    }
}
