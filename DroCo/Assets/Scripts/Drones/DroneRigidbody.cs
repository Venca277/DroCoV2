using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneRigidbody : MonoBehaviour {

    [SerializeField]
    protected Rigidbody RigidBody;

    [SerializeField]
    private float Weight = 0.907f;

    protected float startDrag;
    protected float startAngularDrag;

    private void Awake() {
        if (RigidBody) {
            RigidBody.mass = Weight;
            startDrag = RigidBody.drag;
            startAngularDrag = RigidBody.angularDrag;
        }
    }

    private void FixedUpdate() {
        if (!RigidBody) {
            return;
        }

        HandlePhysics();
    }

    protected virtual void HandlePhysics() {
    }

}
