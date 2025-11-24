using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DistanceBillboard : MonoBehaviour {

    public Transform ObjectToFace;
    public Transform ObjectToComputeDistance;

    public TMP_Text DistanceText;

    private void Start() {
        ObjectToFace = Camera.main.transform;
        if (ObjectToComputeDistance == null) {
            ObjectToComputeDistance = ObjectToFace;
        }
        MissionManager.Instance.AddToDistanceList(this);
    }

    private void Update() {
        transform.LookAt(ObjectToFace.transform);
        DistanceText.text = Vector3.Distance(transform.position, ObjectToComputeDistance.transform.position).ToString("F2") + " m";
    }
}
