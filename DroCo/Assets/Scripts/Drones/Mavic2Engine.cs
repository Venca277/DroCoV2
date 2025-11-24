using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mavic2Engine : MonoBehaviour, IEngine {

    [Header("Engine Properties")]
    [SerializeField] private float MaxPower = 4f;
    [SerializeField] private float LerpSpeed = 2f;

    [Header("Propeller properties")]
    [SerializeField] private Transform Propeller;
    [SerializeField] private float PropellerRotationSpeed = 20f;

    private float FinalEngineForce;

    public void InitEngine() {
        throw new System.NotImplementedException();
    }

    public void UpdateEngine(Rigidbody rigidbody, Mavic2Inputs input) {
        /* Opposite force:
        *  You can add to engineForce a force opposite to the gravity;this makes flying the UAV a bit easier.
        *  F = -Fg = rigidbody.mass * Physics.gravity.magnitude
        */
        Vector3 upVec = transform.up;
        upVec.x = 0f;
        upVec.z = 0f;
        float diff = 1 - upVec.magnitude;
        float finalDiff = Physics.gravity.magnitude * diff;

        Vector3 engineForce = Vector3.zero;

        engineForce = transform.up * (rigidbody.mass * Physics.gravity.magnitude + finalDiff + (input.Throttle * MaxPower)) / 4f;

        rigidbody.AddForce(engineForce, ForceMode.Force);

        //float engineForce = input.Throttle * MaxPower / 4f;

        //if (engineForce == 0f) {
        //    FinalEngineForce = 0f;
        //    rigidbody.velocity = Vector3.zero;
        //} else {
        //}

        //Debug.Log("Engine force " + engineForce);
        //Debug.Log("Final engine force " + FinalEngineForce);
        //Debug.Log("Velocity " + rigidbody.velocity.ToString());


        //FinalEngineForce = Mathf.Lerp(FinalEngineForce, engineForce, Time.deltaTime * LerpSpeed);

        //rigidbody.AddForce(transform.up * FinalEngineForce, ForceMode.Force);

        //rigidbody.AddForce(transform.up * (rigidbody.mass * Mathf.Abs(Physics.gravity.y) / 4f));
        

        HandlePropeller();
    }

    public void HandlePropeller() {
        if (!Propeller) {
            return;
        }

        Propeller.Rotate(Vector3.forward, PropellerRotationSpeed);
    }
}
