using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowSimple : MonoBehaviour {

    public Transform transformToFollow;
    public Transform cameraToAlign;
    public float heightLookModification = 7f;

    private bool followingTarget;
    private Vector3 velocity = Vector3.zero;
    private float rotationSpeed = 10;

    private void LateUpdate() {
        if (followingTarget && transformToFollow != null && cameraToAlign != null) {
            //transform.position = transformToFollow.position;
            //transform.rotation = transformToFollow.rotation;
            transform.position = Vector3.SmoothDamp(transform.position, cameraToAlign.position, ref velocity, 0.3f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation((transformToFollow.position + new Vector3(0f, heightLookModification, 0f)) - transform.position), rotationSpeed * Time.deltaTime);
            //transform.LookAt(transformToFollow.position + new Vector3(0f, heightLookModification, 0f));
            //transform.rotation = transformToFollow.rotation;
        }
    }

    public void StartFollowing(Transform tf, Transform cam) {
        transformToFollow = tf;
        cameraToAlign = cam;
        followingTarget = true;
    }

    public void StopFollowing() {
        transformToFollow = null;
        followingTarget = false;
    }
}
