using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PropellerRotation : MonoBehaviour {

    public bool ClockwiseRotation = true;
    public float RotationSpeed = 1f;

    private void Start() {
        if (!ClockwiseRotation) {
            RotationSpeed *= -1;
        }
    }


    private void Update() {
        transform.Rotate(Vector3.forward, RotationSpeed * Time.deltaTime);
    }

}
