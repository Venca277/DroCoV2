using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WaypointGameObject : MonoBehaviour {

    [SerializeField]
    private RectTransform connectionBinder;
    public RectTransform ConnectionBinder => connectionBinder;

    [SerializeField]
    private TMP_Text text;

    [SerializeField]
    private GameObject shadow;


    private void Update() {
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out RaycastHit hit)) {
            if (hit.collider != null) {
                if (hit.collider.gameObject.layer != 12) {
                    shadow.transform.localScale = new Vector3(shadow.transform.localScale.x, hit.distance / 2, shadow.transform.localScale.z);
                }
            }
        }
    }

    public void SetText(string txt) {
        text.text = txt;
    }
}
