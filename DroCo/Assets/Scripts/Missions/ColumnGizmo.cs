using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class ColumnGizmo : Singleton<ColumnGizmo> {
    private Camera cam;
    public float screensize = 0.05f;
    private GameObject groundArrowsContainer;

    enum ArrowType {
        X, Y, Z
    };

    private GameObject xArrow;
    private GameObject yArrow;
    private GameObject zArrow;
    private Renderer[] xgrafic;
    private Renderer[] ygrafic;
    private Renderer[] zgrafic;

    private ArrowType? draggingArrow;
    private Vector3 startPos;
    private MissionEditor missioneditor;
    private Plane plane;
    public Transform wp;
    private List<WaypointSelect> Waypointscol = new List<WaypointSelect>();
    public bool dragging => draggingArrow != null;
    void Start() {
        cam = Camera.main;
        missioneditor = FindObjectOfType<MissionEditor>();
    }

    void Update() {
        //decide which arrow we drag
        if (Input.GetMouseButtonDown(0)) {
            Ray r = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(r, out hit)) {
                Transform trans = hit.transform;
                while (trans != null) {
                    if (trans.gameObject == xArrow) {
                        Dragging(ArrowType.X);
                        break;
                    } else if (trans.gameObject == yArrow) {
                        Dragging(ArrowType.Y);
                        break;
                    } else if (trans.gameObject == zArrow) {
                        Dragging(ArrowType.Z);
                        break;
                    }
                    trans = trans.parent;
                }
            }
        }

        if (Input.GetMouseButton(0) && draggingArrow != null) {
            UpdateDragging();
        }

        if (Input.GetMouseButtonUp(0)) {
            draggingArrow = null;
        }
    }

    public void createColumn(WaypointSelect clickedWp) {
        wp = clickedWp.transform;
        GameObject columnUp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        GameObject columnDown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        columnUp.name = "ColumnUp";
        columnDown.name = "ColumnDown";
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.5f, 0f);
        columnUp.GetComponent<Renderer>().material = mat;
        columnDown.GetComponent<Renderer>().material = mat;
        Destroy(columnUp.GetComponent<Collider>());
        Destroy(columnDown.GetComponent<Collider>());
        columnDown.layer = LayerMask.NameToLayer("Mission");
        columnUp.layer = LayerMask.NameToLayer("Mission");
        columnUp.transform.localPosition = Vector3.zero;
        columnUp.transform.localScale = new Vector3(0.1f, 1f, 0.06f);
        columnDown.transform.localPosition = Vector3.zero;
        columnDown.transform.localScale = new Vector3(0.1f, 1f, 0.06f);
        RaycastHit[] hits = Physics.RaycastAll(clickedWp.transform.position, Vector3.down, 1000f, LayerMask.GetMask("Mission", "Default"));
        RaycastHit[] hitsUp = Physics.RaycastAll(clickedWp.transform.position, Vector3.up, 1000f, LayerMask.GetMask("Mission"));


        if (hits.Length > 0) {
            RaycastHit lowestHit = hits[0];
            WaypointSelect highestWp = clickedWp;
            float highestY = clickedWp.transform.position.y;
            Waypointscol.Clear();
            Waypointscol.Add(clickedWp);
            foreach (RaycastHit hit in hits) {
                if (lowestHit.point.y > hit.point.y) {
                    lowestHit = hit;
                }
                if (hit.transform.GetComponent<WaypointSelect>() != null && !Waypointscol.Contains(hit.transform.GetComponent<WaypointSelect>())) {
                    Waypointscol.Add(hit.transform.GetComponent<WaypointSelect>());
                }
            }
            foreach (RaycastHit hit in hitsUp) {
                WaypointSelect wp = hit.transform.GetComponent<WaypointSelect>();
                if (wp != null && wp != clickedWp && hit.point.y > highestY) {
                    highestY = hit.point.y;
                    highestWp = wp;
                }
                if (wp != null && !Waypointscol.Contains(wp)) {
                    Waypointscol.Add(wp);
                }
            }

            //set pipe to the ground hit
            columnDown.transform.position = (lowestHit.point + clickedWp.transform.position) / 2;
            float dist = Vector3.Distance(clickedWp.transform.position, lowestHit.point);
            columnDown.transform.localScale = new Vector3(0.1f, dist / 2, 0.06f);
            columnDown.transform.up = (clickedWp.transform.position - lowestHit.point).normalized;

            //set the pipe to the sky
            columnUp.transform.position = (clickedWp.transform.position + highestWp.transform.position) / 2;
            float distUp = Vector3.Distance(clickedWp.transform.position, highestWp.transform.position);
            columnUp.transform.localScale = new Vector3(0.1f, distUp / 2, 0.06f);
            columnUp.transform.up = (highestWp.transform.position - clickedWp.transform.position).normalized;
            Debug.Log("Created column");

            columnDown.transform.parent = clickedWp.transform;
            columnUp.transform.parent = clickedWp.transform;

            HandleArrows arrowsInst = FindObjectOfType<HandleArrows>();
            if (arrowsInst != null) {
                xArrow = arrowsInst.createArrow(Vector3.right, Color.red);
                yArrow = arrowsInst.createArrow(Vector3.up, Color.green);
                zArrow = arrowsInst.createArrow(Vector3.forward, Color.blue);

                groundArrowsContainer = new GameObject("GroundArrows");
                groundArrowsContainer.transform.position = lowestHit.point;
                xArrow.transform.parent = groundArrowsContainer.transform;
                yArrow.transform.parent = groundArrowsContainer.transform;
                zArrow.transform.parent = groundArrowsContainer.transform;

                xArrow.transform.localScale = new Vector3(2f, 2f, 2f);
                yArrow.transform.localScale = new Vector3(2f, 2f, 2f);
                zArrow.transform.localScale = new Vector3(2f, 2f, 2f);
                xArrow.transform.localPosition = Vector3.zero;
                yArrow.transform.localPosition = Vector3.zero;
                zArrow.transform.localPosition = Vector3.zero;
                xgrafic = xArrow.GetComponentsInChildren<Renderer>();
                ygrafic = yArrow.GetComponentsInChildren<Renderer>();
                zgrafic = zArrow.GetComponentsInChildren<Renderer>();
            }
        }
    }

    private void LateUpdate() {
        if (cam == null)
            return;

        if (groundArrowsContainer != null) {
            float distance = Vector3.Distance(cam.transform.position, groundArrowsContainer.transform.position);
            float newScale = distance * screensize;
            groundArrowsContainer.transform.localScale = Vector3.one * newScale;
        }
    }

    private void Dragging(ArrowType type) {
        ResetHighlight(xgrafic);
        ResetHighlight(ygrafic);
        ResetHighlight(zgrafic);

        draggingArrow = type;
        startPos = groundArrowsContainer.transform.position;

        //on what plane to drag on
        Vector3 plane;
        if (type == ArrowType.X) {
            plane = Vector3.up;
            SetHighlight(xgrafic, Color.red);
        } else if (type == ArrowType.Y) {
            plane = Vector3.forward;
            SetHighlight(ygrafic, Color.green);
        } else {
            plane = Vector3.up;
            SetHighlight(zgrafic, Color.blue);
        }

        this.plane = new Plane(plane, groundArrowsContainer.transform.position);

        //init drag in mission editor
        if (missioneditor != null) {
            missioneditor.InitDragColumn(Waypointscol);
        }
    }

    private void UpdateDragging() {
        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        float dist;

        //if ray hits plane we move the arrow
        if (plane.Raycast(r, out dist)) {
            Vector3 hitplace = r.GetPoint(dist);
            Vector3 diff = hitplace - startPos;

            //move arrow and waypoint
            Vector3 offset = Vector3.zero;
            if (draggingArrow == ArrowType.X) {
                offset = new Vector3(diff.x, 0, 0);
            } else if (draggingArrow == ArrowType.Y) {
                offset = new Vector3(0, diff.y, 0);
            } else if (draggingArrow == ArrowType.Z) {
                offset = new Vector3(0, 0, diff.z);
            }

            //groundArrowsContainer.transform.position += offset;
            missioneditor.MoveWaypointColumn(Waypointscol, offset);
            startPos = hitplace;
        }
    }

    private void ResetHighlight(Renderer[] grafics) {
        foreach (Renderer r in grafics) {
            Material mat = r.material;
            mat.DisableKeyword("_EMISSION");
        }
    }

    private void SetHighlight(Renderer[] grafics, Color color) {
        foreach (Renderer g in grafics) {
            Material mat = g.material;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 30f);
        }
    }
}
