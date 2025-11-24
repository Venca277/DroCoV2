using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CameraManager : Singleton<CameraManager> {
    public enum CameraView {
        FirstPerson,
        FreeLook,
        None
    }

    [SerializeField]
    private bool UseARCameraSwitch = false;

    [SerializeField]
    private Camera MainCamera;

    private ArcGISCameraControllerTouch cameraControllerTouch;
    private CameraFollowSimple cameraFollow;

    public bool FollowingTarget { get; private set; }

    private float pinchSpeed = 0.03f;

    public GameObject UI;
    private InteractiveObject droneFPV;

    public GameObject ARCameraBackground;
    private RawImage arCameraBackgroundImage;

    private bool vrSceneActive = false;

    private CameraView currentCameraView = CameraView.FreeLook;
    private Shader buildingShader;
    private Shader buildingShaderLit;

    private void Start() {
        cameraControllerTouch = MainCamera.GetComponent<ArcGISCameraControllerTouch>();
        cameraFollow = MainCamera.GetComponent<CameraFollowSimple>();
        FollowingTarget = false;

        arCameraBackgroundImage = ARCameraBackground.GetComponentInChildren<RawImage>();

        buildingShader = UnityEngine.Shader.Find("Custom/MobileOcclusion");
        buildingShaderLit = UnityEngine.Shader.Find("Shader Graphs/SceneNodeSurface");

        SetCameraView(CameraView.FreeLook);
    }


    private void Update() {
        if (GetMouseScollValue() != 0f && FollowingTarget) {
            if (GameManager.Instance.CurrentDisplayState == GameManager.DisplayState.Scene3DView) {
                StopFollowingTarget();
            }
        }

        if (UseARCameraSwitch) {
            if (Keyboard.current[Key.Numpad1].wasPressedThisFrame) {
                SetCameraView(CameraView.FirstPerson);
                //} else if (Keyboard.current[Key.Numpad2].wasPressedThisFrame) {
                //    SetCameraView(CameraView.ThirdPerson);
            } else if (Keyboard.current[Key.Numpad3].wasPressedThisFrame) {
                SetCameraView(CameraView.FreeLook);
            } 
        }
    }

    private void DisplayVRScene(bool active = true) {
        if (active) {
            foreach (GameObject building in FindGameObjectsInLayer(10)) {
                building.GetComponent<MeshRenderer>().material.shader = buildingShaderLit;
            }
        } else {
            foreach (GameObject building in FindGameObjectsInLayer(10)) {
                building.GetComponent<MeshRenderer>().material.shader = buildingShader;
            }
        }
    }

    private GameObject[] FindGameObjectsInLayer(int layer) {
        var goArray = Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[];
        var goList = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < goArray.Length; i++) {
            if (goArray[i].layer == layer) {
                goList.Add(goArray[i]);
            }
        }
        if (goList.Count == 0) {
            return null;
        }
        return goList.ToArray();
    }

    private float GetMouseScollValue() {
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 2) {
            UnityEngine.InputSystem.EnhancedTouch.Touch touch1 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
            UnityEngine.InputSystem.EnhancedTouch.Touch touch2 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[1];

            Vector2 touch1Previous = touch1.screenPosition - touch1.delta;
            Vector2 touch2Previous = touch2.screenPosition - touch2.delta;

            return -pinchSpeed * (Vector2.Distance(touch1Previous, touch2Previous) - Vector2.Distance(touch1.screenPosition, touch2.screenPosition));
        } else if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0) {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current.scroll.ReadValue().normalized.y;
#else
            return Input.mouseScrollDelta.y;
#endif
        } else {
            return 0f;
        }
    }

    public void StartFollowingTarget(Transform transformToFollow, Transform cameraToAlign) {
        cameraControllerTouch.enabled = false;
        cameraFollow.enabled = true;
        cameraFollow.StartFollowing(transformToFollow, cameraToAlign);
        FollowingTarget = true;
    }

    public void StopFollowingTarget() {
        cameraControllerTouch.enabled = true;
        cameraFollow.enabled = false;
        cameraFollow.StopFollowing();
        FollowingTarget = false;
    }

    public void SetCameraView(CameraView view) {
        if (droneFPV == null && DroneManager.Instance.Drones.Count >= 1) {
            IEnumerator enumerator = DroneManager.Instance.Drones.Values.GetEnumerator();
            enumerator.MoveNext();
            droneFPV = (InteractiveObject) enumerator.Current;
        }
        if (droneFPV != null) {
            switch (view) {
                case CameraView.FirstPerson:
                    UnsetFreeLook();
                    SetFPV(droneFPV);
                    break;
                case CameraView.FreeLook:
                    UnsetFPV(droneFPV);
                    SetFreeLook();
                    break;
            }
        }
    }

    private void SetFPV(InteractiveObject firstPerson) {
        firstPerson.FPVOnlyCamera.gameObject.SetActive(true);
        firstPerson.SetARBackground(arCameraBackgroundImage);
        firstPerson.FPVOnlyCamera.tag = "MainCamera";
        ARCameraBackground.SetActive(true);
        ARCameraBackground.GetComponent<Canvas>().worldCamera = firstPerson.FPVOnlyCamera;
        arCameraBackgroundImage.texture = firstPerson.GetCameraTexture();
        currentCameraView = CameraView.FirstPerson;
    }

    private void UnsetFPV(InteractiveObject firstPerson) {
        firstPerson.FPVOnlyCamera.gameObject.SetActive(false);
        firstPerson.SetARBackground(null);
        firstPerson.FPVOnlyCamera.tag = "Untagged";
        ARCameraBackground.SetActive(false);
        currentCameraView = CameraView.None;
    }

    private void SetFreeLook() {
        MainCamera.gameObject.SetActive(true);
        DroneManager.Instance.SetDroneModelsActive(true);
        UI.SetActive(true);
        MainCamera.tag = "MainCamera";
        DisplayVRScene(true);
        currentCameraView = CameraView.FreeLook;
    }

    private void UnsetFreeLook() {
        MainCamera.gameObject.SetActive(false);
        DroneManager.Instance.SetDroneModelsActive(false);
        UI.SetActive(false);
        MainCamera.tag = "Untagged";
        DisplayVRScene(false);
        currentCameraView = CameraView.None;
    }

    private Matrix4x4 CreateProjectionMatrix() {
        // Intrinsic parameters
        Matrix4x4 intrinsicMatrix = new Matrix4x4(
            new Vector4(1015.9761352539063f, 0.0f, 630.6932373046875f, 0.0f),
            new Vector4(0.0f, 1017.92529296875f, 346.8971862792969f, 0.0f),
            new Vector4(0.0f, 0.0f, 1.0f, 0.0f),
            new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
        );

        // Extrinsic parameters (assumed identity rotation and zero translation)
        Matrix4x4 extrinsicMatrix = new Matrix4x4(
            new Vector4(1.0f, 0.0f, 0.0f, 0.0f),
            new Vector4(0.0f, 1.0f, 0.0f, 0.0f),
            new Vector4(0.0f, 0.0f, 1.0f, 0.0f),
            new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
        );

        // Full projection matrix (P = K * [R | t])
        Matrix4x4 projectionMatrix = intrinsicMatrix * extrinsicMatrix;

        Debug.Log("Projection Matrix: " + projectionMatrix);

        return projectionMatrix;
    }

    public void SetCurrentInteractiveObject(InteractiveObject intObject) {
        droneFPV = intObject;
    }

    public void SwitchCameraView() {
        SetCameraView(currentCameraView == CameraView.FreeLook ? CameraView.FirstPerson : CameraView.FreeLook);
    }

}
