using System.Collections;
using System.Collections.Generic;
using Esri.ArcGISMapsSDK.Components;
using Esri.HPFramework;
using Unity.Mathematics;
using UnityEngine;

public class CameraFollow : MonoBehaviour {

    public enum LookDirection {
        FRONT,
        BACK,
        LEFT,
        RIGHT
    }

    public enum CameraMode {
        COCKPIT,
        FOLLOW
    }

    public Transform Target;

    public CameraMode CameraFollowMode;

    //public float DistanceFromObject;
    //public float CameraHeight;
    public LookDirection ObjectVisibleFrom;

    public bool LookRotation;
    public Vector3 LookRotationVector = new Vector3();

    private LookDirection currentVisibleFrom;


    private float smoothSpeed = 0.1f;
    private Vector3 cameraOffset;

    private Vector3 cameraRelativePosition;
    private Vector3 cameraRelativeFwDirection;
    private Vector3 cameraRelativeUpDirection;

    private Vector3 lookDirectionP;
    private Quaternion lookDirectionR;

    private void Start() {
        currentVisibleFrom = ObjectVisibleFrom;
        ChangeCameraDirection(ObjectVisibleFrom);
    }

    private void Update() {
        //hack for debug, remove
        if (currentVisibleFrom != ObjectVisibleFrom) {
            ChangeCameraDirection(ObjectVisibleFrom);
            currentVisibleFrom = ObjectVisibleFrom;
        }

        if (CameraFollowMode == CameraMode.COCKPIT) {
            UpdateCameraCockpit();
        } else if (CameraFollowMode == CameraMode.FOLLOW) {
            UpdateCameraFollow();
        }
    }

    private void UpdateCameraCockpit() {
        Vector3 updatedRelativePosition = Target.TransformPoint(cameraRelativePosition);
        Vector3 updatedRelativeFwDirection = Target.TransformDirection(cameraRelativeFwDirection);
        Vector3 updatedRelativeUpDirection = Target.TransformDirection(cameraRelativeUpDirection);
        Quaternion updatedRotation = Quaternion.LookRotation(updatedRelativeFwDirection, updatedRelativeUpDirection);
        transform.position = updatedRelativePosition;
        transform.rotation = updatedRotation;
    }

    private void UpdateCameraFollow() {
        Quaternion rot = Quaternion.Euler(0f, Target.rotation.eulerAngles.y, 0f);
        Vector3 updatedRelativePosition = Target.position + rot * cameraOffset;
        transform.position = updatedRelativePosition;
        transform.LookAt(Target);
        if (LookRotation) {
            transform.eulerAngles = LookRotationVector + new Vector3(0f, transform.eulerAngles.y, 0f);
        } else {
            transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f);
        }
    }

    public void ChangeCameraDirection(LookDirection direction) {
        switch (direction) {
            case LookDirection.BACK:
                lookDirectionP = new Vector3(-10f, 4f, 0f);
                lookDirectionR = Quaternion.Euler(0f, 90f, 0f);
                break;
            case LookDirection.FRONT:
                lookDirectionP = new Vector3(10f, 4f, 0f);
                lookDirectionR = Quaternion.Euler(0f, -90f, 0f);
                break;
            case LookDirection.LEFT:
                lookDirectionP = new Vector3(0f, 4f, -10f);
                lookDirectionR = Quaternion.Euler(0f, 0f, 0f);
                break;
            case LookDirection.RIGHT:
                lookDirectionP = new Vector3(0f, 4f, 10f);
                lookDirectionR = Quaternion.Euler(0f, 180f, 0f);
                break;
        }
        transform.position = Target.TransformPoint(lookDirectionP);
        transform.rotation = Target.rotation * lookDirectionR;

        cameraOffset = lookDirectionP;
        cameraRelativePosition = Target.InverseTransformPoint(transform.position);
        cameraRelativeFwDirection = Target.InverseTransformDirection(transform.forward);
        cameraRelativeUpDirection = Target.InverseTransformDirection(transform.up);
    }
}
