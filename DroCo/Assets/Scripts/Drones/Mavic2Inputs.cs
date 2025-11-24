using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Mavic2Inputs : MonoBehaviour {
    private Vector2 cyclic;

    private float throttle;

    private float pedals;

    public Vector2 Cyclic {
        get => cyclic;
    }

    public float Throttle {
        get => throttle;
    }

    public float Pedals {
        get => pedals;
    }

    private void OnCyclic(InputValue value) {
        cyclic = value.Get<Vector2>();
    }

    private void OnThrottle(InputValue value) {
        throttle = value.Get<float>();
    }

    private void OnPedals(InputValue value) {
        pedals = value.Get<float>();
    }
}
