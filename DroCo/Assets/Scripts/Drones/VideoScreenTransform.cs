using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VideoScreenTransform : MonoBehaviour {
    //private Quaternion lastParentRotation;

    //private void Start() {
    //    lastParentRotation = transform.parent.localRotation;
    //}

    //private void Update() {
        
    //    //transform.rotation = transform.rotation * Quaternion.Inverse(transform.parent.rotation);
    //    //transform.LookAt(transform.parent);
    //    //transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f);
    //    //transform.rotation = Quaternion.Euler(transform.parent.rotation.x * -1f, transform.parent.rotation.y, transform.parent.rotation.z * -1f);
    //}

    private void LateUpdate() {
        //transform.localRotation = Quaternion.Inverse(transform.parent.localRotation) * lastParentRotation * transform.localRotation;
        //lastParentRotation = transform.parent.localRotation;
    }
}
