using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaypointSelect : MonoBehaviour {

    private Renderer rend;
    public Color originalColor;

    [Header("Settings")]
    public Color selectedColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void Awake() {
        rend = GetComponent<Renderer>();
        if (rend != null) {
            originalColor = new Color(1f, 0.84f, 0f, 1f);
            rend.material.color = originalColor;
        }
    }

    public void Select(bool selected) {
        if (rend != null) {
            rend.material.color = selected ? selectedColor : originalColor;
        }
    }

}