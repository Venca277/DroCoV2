using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour {

    public bool BuildingCorrected => Mathf.Approximately(transform.localScale.y, correctionScale) && buildingCorrected;

    private float correctionScale = 0f;
    private bool buildingCorrected = false;

    public void SetScale(float scale) {
        correctionScale = scale;
    }

    public void CorrectBuilding(float scale, float altitudeOffset) {
        transform.localScale = new Vector3(1f, scale, 1f);
        transform.position += new Vector3(0f, altitudeOffset, 0f);
        gameObject.layer = LayerMask.NameToLayer("Buildings");
        buildingCorrected = true;
    }

    public void NotABuilding() {
        transform.localScale = new Vector3(1f, 1f, 1f);
        transform.position += new Vector3(0f, 0f, 0f);
        gameObject.layer = LayerMask.NameToLayer("Default");
        buildingCorrected = true;
    }

}
