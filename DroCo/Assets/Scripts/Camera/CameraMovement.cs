using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour {
    public enum Gesture {
        NONE = 0,
        PAN = 1,
        TILT = 2,
        ZOOM = 3,
        ROTATE = 4
    }
        
#if UNITY_IOS || UNITY_ANDROID
    public Camera Camera;
    public Transform SceneToMoveAround;
    public bool Rotate;
    public float TiltSpeed = 0.1f;

    protected Plane plane;
    private Vector3 planePosition;

    public LayerMask MapLayer;

    public Vector2 ReferenceResolution = new Vector2(2560f, 1600f);

    private Gesture currentGesture; 

    private void Awake() {
        if (Camera == null)
            Camera = Camera.main;
    }

    private void Start() {
        planePosition = SceneToMoveAround.position;

        currentGesture = Gesture.NONE;
    }

    private void Update() {

        Vector3 delta1 = Vector3.zero;
        Vector3 delta2 = Vector3.zero;

        //Pan
        if (Input.touchCount == 1) {
            delta1 = PlanePositionDelta(Input.GetTouch(0));
            if (Input.GetTouch(0).phase == TouchPhase.Moved) {

                Vector3 camOrigPosition = Camera.transform.position;

                Camera.transform.Translate(delta1, Space.World);

                (bool, Vector3) pose = CheckIfPositionEligible(Camera.transform.position, Camera.transform);
                if (!pose.Item1) {
                    Camera.transform.Translate(Vector3.up * (10f - (camOrigPosition.y - pose.Item2.y)));
                }
            }
        }        
        else if (Input.touchCount >= 2) {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);

            //Tilt
            if (Vector2.Distance(ConvertToReferenceResolution(touch1.position), ConvertToReferenceResolution(touch2.position)) <= 350f &&
                Mathf.Abs(touch1.deltaPosition.x) <= 5f &&
                Mathf.Abs(touch2.deltaPosition.x) <= 5f &&
                ((touch1.deltaPosition.y < 0f && touch2.deltaPosition.y < 0f) ||
                (touch1.deltaPosition.y > 0f && touch2.deltaPosition.y > 0f))) {

                currentGesture = Gesture.TILT;

                Vector3 camOrigPosition = Camera.transform.position;

                Camera.transform.eulerAngles = new Vector3(Mathf.Clamp(Camera.transform.eulerAngles.x + (-touch1.deltaPosition.y * TiltSpeed), 0f, 90f), Camera.transform.eulerAngles.y, Camera.transform.eulerAngles.z);
                
                (bool, Vector3) pose = CheckIfPositionEligible(Camera.transform.position, Camera.transform);

                if (pose.Item1) {
                    Camera.transform.position = camOrigPosition;
                }

            } else if (currentGesture != Gesture.TILT) {

                Vector3 pos1 = PlanePosition(touch1.position);
                Vector3 pos2 = PlanePosition(touch2.position);
                Vector3 pos1b = PlanePosition(touch1.position - touch1.deltaPosition);
                Vector3 pos2b = PlanePosition(touch2.position - touch2.deltaPosition);

                //calc zoom
                float zoom = Vector3.Distance(pos1, pos2) /
                           Vector3.Distance(pos1b, pos2b);

                //edge case
                if (zoom == 0 || zoom > 10)
                    return;

                Vector3 camOrigPosition = Camera.transform.position;

                //Zoom
                Vector3 position = Vector3.LerpUnclamped(pos1, Camera.transform.position, 1 / zoom);
                if (!float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsNaN(position.z)) {

                    (bool, Vector3) pose = CheckIfPositionEligible(position, Camera.transform);
                    if (pose.Item1) {
                        Camera.transform.position = position;
                    }
                }

                //Rotation
                if (Rotate && pos2b != pos2) {
                    camOrigPosition = Camera.transform.position;
                    Camera.transform.RotateAround(pos1, plane.normal, Vector3.SignedAngle(pos2 - pos1, pos2b - pos1b, plane.normal));

                    (bool, Vector3) pose = CheckIfPositionEligible(Camera.transform.position, Camera.transform);
                    if (!pose.Item1) {
                        Camera.transform.Translate(Vector3.up * (10f - (camOrigPosition.y - pose.Item2.y)));
                    }
                }
            }
        } else {
            currentGesture = Gesture.NONE;
        }
    }

    private Vector2 ConvertToReferenceResolution(Vector2 point) {
        return new Vector2(ReferenceResolution.x / Screen.width * point.x, ReferenceResolution.y / Screen.height * point.y);
    }

    protected Vector3 PlanePositionDelta(Touch touch) {

        if (touch.phase != TouchPhase.Moved)
            return Vector3.zero;

        Ray rayBefore = Camera.ScreenPointToRay(touch.position - touch.deltaPosition);
        Ray rayNow = Camera.ScreenPointToRay(touch.position);

        Vector3 planePositionBefore = RaycastMap(rayBefore);
        Vector3 planePosition = RaycastMap(rayNow);

        UpdatePlane(SceneToMoveAround.up, planePosition);

        return planePositionBefore - planePosition;
    }

    protected Vector3 PlanePosition(Vector2 screenPos) {        
        Ray rayNow = Camera.ScreenPointToRay(screenPos);

        Vector3 planePosition = RaycastMap(rayNow);

        UpdatePlane(SceneToMoveAround.up, planePosition);

        return planePosition;
    }

    private Vector3 RaycastMap(Ray ray) {
        Vector3 planePoint = Vector3.zero;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance:Mathf.Infinity, layerMask:MapLayer.value)) {            
            planePoint = hit.point;
        }

        return planePoint;
    }

    private void UpdatePlane(Vector3 normal, Vector3 position) {
        plane.SetNormalAndPosition(normal, position);
    }

    private (bool, Vector3) CheckIfPositionEligible(Vector3 positionToCheck, Transform objectToTranslate, float rayDistance = 10f) {
        RaycastHit hit;

        if (Physics.Raycast(positionToCheck, objectToTranslate.forward, out hit, maxDistance: rayDistance, layerMask: MapLayer.value)) {
            return (false, hit.point);
        }
        if (Physics.Raycast(positionToCheck, -objectToTranslate.forward, out hit, maxDistance: rayDistance, layerMask: MapLayer.value)) {
            return (false, hit.point);
        }
        if (Physics.Raycast(positionToCheck, -objectToTranslate.up, out hit, maxDistance: rayDistance, layerMask: MapLayer.value)) {
            return (false, hit.point);
        }
        if (Physics.Raycast(positionToCheck, objectToTranslate.right, out hit, maxDistance: rayDistance, layerMask: MapLayer.value)) {
            return (false, hit.point);
        }
        if (Physics.Raycast(positionToCheck, -objectToTranslate.right, out hit, maxDistance: rayDistance, layerMask: MapLayer.value)) {
            return (false, hit.point);
        }

        return (true, Vector3.zero);
    }

#endif

}
