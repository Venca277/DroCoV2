using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboarding : MonoBehaviour {

    public Transform ObjectToFace;

    private void Update() {
        transform.LookAt(ObjectToFace == null ? Camera.main.transform : ObjectToFace);
    }
}
