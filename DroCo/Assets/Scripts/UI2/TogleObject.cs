using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TogleObject : MonoBehaviour {
    public GameObject targetObject;

    public void Toggle() {
        if (targetObject != null) {
            targetObject.SetActive(!targetObject.activeSelf);
        }
    }
}
