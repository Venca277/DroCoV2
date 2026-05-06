// ============================================================
// BuildingHover.cs
//
// Author:  Václav Sovák
// Date:    2026-04-05
//
// Highlights preloaded buildings on mouse hover and elevates
// the building up. Skips the currently selected building.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingHover : MonoBehaviour {
    static int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public Camera arcgisCam;
    GameObject lastHov;
    Material lastMat;
    public bool lift = false;
    public GameObject selected;
    private GameObject liftCopy;
    private Vector3 lastPos;

    void Update() {
        Ray ray = arcgisCam.ScreenPointToRay(Input.mousePosition);

        //find building under mouse
        GameObject current = null;
        foreach (var hit in Physics.RaycastAll(ray, Mathf.Infinity)) {
            if (hit.collider.GetComponent<BuildingTag>() != null && hit.collider.gameObject != selected) {
                current = hit.collider.gameObject;
                break;
            }
        }

        //elevate current hover
        if (current == lastHov) {
            if (lift && liftCopy != null)
                liftCopy.transform.position = Vector3.Lerp(
                    liftCopy.transform.position,
                    lastHov.transform.position + Vector3.up * 15f,
                    Time.deltaTime * 3f);
            return;
        }

        //hover changed destroy previous hover
        if (liftCopy != null) {
            Destroy(liftCopy);
            liftCopy = null;
        }

        //set previous hover to visible
        if (lastHov != null) {
            MeshRenderer prevMr = lastHov.GetComponent<MeshRenderer>();
            if (prevMr != null)
                prevMr.enabled = true;
        }

        lastHov = current;

        if (current != null) {
            MeshFilter mf = current.GetComponent<MeshFilter>();
            MeshRenderer mr = current.GetComponent<MeshRenderer>();
            if (mf != null && mr != null) {
                //create copy with emission material
                liftCopy = new GameObject("BuildingHoverCopy");
                liftCopy.transform.SetPositionAndRotation(current.transform.position, current.transform.rotation);
                liftCopy.transform.localScale = current.transform.localScale;

                liftCopy.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;

                //apply emission color to material
                Material mat = new Material(mr.sharedMaterial);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColor, new Color(0f, 0.4f, 1f) * 1f);
                mat.SetFloat("_Smoothness", 0.6f);
                liftCopy.AddComponent<MeshRenderer>().material = mat;
                liftCopy.layer = LayerMask.NameToLayer("Buildings");

                //hide original
                mr.enabled = false;
            }
        }
    }

    public void ForceReset() {
        //force reset hover state
        if (liftCopy != null) {
            Destroy(liftCopy);
            liftCopy = null;
        }
        if (lastHov != null) {
            MeshRenderer mr = lastHov.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.enabled = true;
            lastHov = null;
        }
    }
}
