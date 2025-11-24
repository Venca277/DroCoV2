using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapUI : MonoBehaviour {

    [SerializeField]
    private Image outline;
    [SerializeField]
    private RawImage minimapCameraView;
    [SerializeField]
    private RawImage fullScreenMinimapCameraView;
    [SerializeField]
    private GameObject fullScreenMinimap;
    [SerializeField]
    private Camera mainCamera;
    [SerializeField]
    private Camera minimapCamera;

    private RenderTexture minimapTexture;
    private RenderTexture sceneViewTexture;

    //private RenderTexture fullScreenMinimapTexture;

    private float lastClick = 0f;
    private float doubleClickInterval = 0.4f;

    private void Start() {
        StartCoroutine(InitMinimapTextures());
    }

    private IEnumerator InitMinimapTextures() {
        yield return new WaitForEndOfFrame();

        minimapTexture = new RenderTexture((int) minimapCameraView.rectTransform.rect.width, (int) minimapCameraView.rectTransform.rect.height, 24);
        sceneViewTexture = new RenderTexture((int) minimapCameraView.rectTransform.rect.width, (int) minimapCameraView.rectTransform.rect.height, 24);
        //fullScreenMinimapTexture = new RenderTexture(Screen.width, Screen.height, 24);
    }

    public void OnPointerEnter() {
        outline.color = Color.white;
    }

    public void OnPointerExit() {
        outline.color = Color.grey;
    }

    public void OnPointerClick() {
        if ((lastClick + doubleClickInterval) > Time.time) {
            GameManager.Instance.SwitchSceneMapView();
        }
        lastClick = Time.time;
    }

    public void SetMinimapView() {
        RecoverCameras();
        mainCamera.targetTexture = sceneViewTexture;
        minimapCameraView.texture = mainCamera.targetTexture;
    }

    public void SetSceneView() {
        RecoverCameras();
        minimapCamera.targetTexture = minimapTexture;
        minimapCameraView.texture = minimapCamera.targetTexture;
    }

    private void RecoverCameras() {
        minimapCamera.targetTexture = null;
        mainCamera.targetTexture = null;
    }

    //private void StretchToFullScreen(bool active) {
    //    fullScreenMinimap.SetActive(active);
    //    minimapCamera.targetTexture = fullScreenMinimapTexture;
    //    fullScreenMinimapCameraView.texture = minimapCamera.targetTexture;
    //}
}
