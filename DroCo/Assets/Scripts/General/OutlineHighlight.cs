using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class OutlineHighlight : MonoBehaviour {

    [SerializeField]
    private GameObject outline;
    [SerializeField]
    private LayerMask highlightLayer;
    [SerializeField]
    private List<GameObject> ObjectsToNotifyClick = new List<GameObject>();

    private Camera mainCamera;

    // Time tracking for double-click detection
    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.3f; // Time interval within which two clicks are considered a double click

    private void Start() {
        mainCamera = Camera.main;
    }


    void Update() {

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, highlightLayer)) {
            if (hit.transform == transform) {
                outline.SetActive(true);

                if (Mouse.current.leftButton.wasPressedThisFrame) {
                    DetectDoubleClick();
                }
            } else {
                outline.SetActive(false);
            }
        } else {
            outline.SetActive(false);
        }


    }

    private void DetectDoubleClick() {
        float currentTime = Time.time;

        if (currentTime - lastClickTime <= doubleClickThreshold) {
            OnObjectClicked();
        }

        lastClickTime = currentTime;
    }

    private void OnObjectClicked() {
        foreach (GameObject notifier in ObjectsToNotifyClick) {
            notifier.GetComponent<IPointerNotifier>()?.OnClicked(IPointerNotifier.ClickObject.DroneScreen);
        }
    }

}
