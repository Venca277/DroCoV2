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
