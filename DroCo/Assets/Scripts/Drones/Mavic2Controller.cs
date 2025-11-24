using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Mavic2Inputs))]
public class Mavic2Controller : DroneRigidbody {

    [SerializeField]
    private float MinMaxPitch = 30f;
    [SerializeField]
    private float MinMaxRoll = 30f;
    [SerializeField]
    private float YawPower = 4f;
    [SerializeField]
    private float LerpSpeed = 2f;

    private Mavic2Inputs input;

    private List<IEngine> engines = new List<IEngine>();

    private float yaw;
    private float finalPitch;
    private float finalRoll;
    private float finalYaw;

    private void Start() {
        input = GetComponent<Mavic2Inputs>();
        engines = GetComponentsInChildren<IEngine>().ToList<IEngine>();
    }

    protected override void HandlePhysics() {
        HandleEngines();
        HandleControls();
    }

    protected virtual void HandleEngines() {
        foreach (IEngine engine in engines) {
            engine.UpdateEngine(RigidBody, input);
        }
    }

    protected virtual void HandleControls() {
        float pitch = -input.Cyclic.x * MinMaxPitch;
        float roll = -input.Cyclic.y * MinMaxRoll;
        yaw += input.Pedals * YawPower;

        finalPitch = Mathf.Lerp(finalPitch, pitch, Time.deltaTime * LerpSpeed);
        finalRoll = Mathf.Lerp(finalRoll, roll, Time.deltaTime * LerpSpeed);
        finalYaw = Mathf.Lerp(finalYaw, yaw, Time.deltaTime * LerpSpeed);

        Quaternion orientation = Quaternion.Euler(finalPitch, finalYaw, finalRoll);
        RigidBody.MoveRotation(orientation);
    }

}
