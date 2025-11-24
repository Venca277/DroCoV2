using System;
using System.Collections;
using System.Collections.Generic;
using Highlighters;
using UnityEngine;
using UnityEngine.UI;

public abstract class InteractiveObject : MonoBehaviour {

    [SerializeField]
    private HighlighterTrigger HighlighterTrigger;

    public Camera TPVCamera;
    public Camera FPVOnlyCamera;
    public Transform DroneModel;

    public RawImage ArCameraBackground = null;

    public virtual void Highlight(bool highlight) {
        HighlighterTrigger.ChangeTriggeringState(highlight);
    }

    public virtual void FocusCamera() {
        CameraManager.Instance.StartFollowingTarget(transform, TPVCamera.transform);
    }

    public virtual void SetCameras() {
        CameraManager.Instance.SetCurrentInteractiveObject(this);
    }

    public abstract Texture GetCameraTexture();

    public abstract void ChangeFlightDataDelay(float delay);

    public virtual void SetARBackground(RawImage arCameraBackgroundImage) {
        ArCameraBackground = arCameraBackgroundImage;
    }
}
