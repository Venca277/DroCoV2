// ============================================================
// HandleArrows.cs
// 
// Author: Václav Sovák
// Date: 2026-04-05
// 
// Creates drag arrows for manipulating a waypoint in scene.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HandleArrows : MonoBehaviour {
    enum ArrowType {
        X, Y, Z
    };

    private GameObject xArrow;
    private GameObject yArrow;
    private GameObject zArrow;

    //renderers for the emission
    private Renderer[] xgrafic;
    private Renderer[] ygrafic;
    private Renderer[] zgrafic;

    private Camera cam;
    private Plane plane;    //plane for dragging the arrows
    public Transform wp;    //waypoint the arrows are manipulating
    private Vector3 startPos;
    private ArrowType? draggingArrow;
    public MissionEditor missioneditor;
    public float screensize = 0.1f;
    public bool dragging => draggingArrow != null;

    void Start() {
        cam = Camera.main;
        //create arrows in 3 axes
        xArrow = CreateArrow(Vector3.right, Color.red);
        yArrow = CreateArrow(Vector3.up, Color.green);
        zArrow = CreateArrow(Vector3.forward, Color.blue);

        //init renderers for emission
        xgrafic = xArrow.GetComponentsInChildren<Renderer>();
        ygrafic = yArrow.GetComponentsInChildren<Renderer>();
        zgrafic = zArrow.GetComponentsInChildren<Renderer>();
    }

    void Update() {

        //detect which arrow is clicked
        if (Input.GetMouseButtonDown(0)) {
            Ray r = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(r, out hit)) {
                if (hit.transform.gameObject == xArrow) {
                    Dragging(ArrowType.X);
                } else if (hit.transform.gameObject == yArrow) {
                    Dragging(ArrowType.Y);
                } else if (hit.transform.gameObject == zArrow) {
                    Dragging(ArrowType.Z);
                }
            }

        }

        //continue drag on holding mouse
        if (Input.GetMouseButton(0) && draggingArrow != null) {
            UpdateDragging();
        }

        //stop drag
        if (Input.GetMouseButtonUp(0)) {
            draggingArrow = null;
        }
    }

    //creates an arrow pointing in axis direction
    public GameObject CreateArrow(Vector3 looksAt, Color color) {
        GameObject arrow = new GameObject("Arrow");
        arrow.transform.parent = transform;
        arrow.transform.localPosition = Vector3.zero;

        //body of the arrow as simple cylinder
        GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stick.transform.parent = arrow.transform;
        stick.transform.localPosition = looksAt * 0.5f;
        stick.transform.localScale = new Vector3(0.05f, 0.5f, 0.1f);

        //tip of the arrow as a cone
        GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        MeshFilter mr = cone.GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        mr.mesh = mesh;

        cone.transform.parent = arrow.transform;
        cone.transform.localPosition = looksAt * 1f;
        cone.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

        //cone geometry
        int segments = 7;
        float angle = 0.0f;
        float angleAmout = 2 * Mathf.PI / segments;
        List<Vector3> verts = new List<Vector3>();
        Vector3 pos = Vector3.zero;

        //top of the cone
        verts.Add(new Vector3(0, 1f, 0));

        //base of the cone
        verts.Add(Vector3.zero);

        //verts of the cone
        for (int i = 0; i < segments; i++) {
            pos.x = 0.5f * Mathf.Sin(angle);
            pos.z = 0.5f * Mathf.Cos(angle);

            verts.Add(new Vector3(pos.x, pos.y, pos.z));
            angle -= angleAmout;
        }

        //rotate cone to look at direction
        Quaternion rotation = Quaternion.identity;
        if (looksAt == Vector3.right) {
            rotation = Quaternion.Euler(0, 0, -90);
        } else if (looksAt == Vector3.forward) {
            rotation = Quaternion.Euler(90, 0, 0);
        }

        //rotate verts
        for (int i = 0; i < verts.Count; i++) {
            verts[i] = rotation * verts[i];
        }

        //set mesh
        mesh.vertices = verts.ToArray();
        List<int> tris = new List<int>();

        //triangles of the cone
        for (int i = 2; i < segments + 1; i++) {
            tris.Add(0);
            tris.Add(i + 1);
            tris.Add(i);
        }

        tris.Add(0);
        tris.Add(2);
        tris.Add(segments + 1);

        //base of the cone
        for (int i = 2; i < segments + 1; i++) {
            tris.Add(1);
            tris.Add(i);
            tris.Add(i + 1);
        }
        tris.Add(1);
        tris.Add(segments + 1);
        tris.Add(2);

        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        //rotation of sticks
        if (looksAt == Vector3.right || looksAt == Vector3.left) {
            stick.transform.localRotation = Quaternion.Euler(0, 0, 90);
        } else if (looksAt == Vector3.forward || looksAt == Vector3.back) {
            stick.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;

        stick.GetComponent<Renderer>().material = mat;
        cone.GetComponent<Renderer>().material = mat;

        //collider for the arrow
        BoxCollider col = arrow.AddComponent<BoxCollider>();
        col.center = looksAt * 0.5f;
        col.size = new Vector3(0.2f, 1.2f, 0.2f);
        if (looksAt == Vector3.right || looksAt == Vector3.left) {
            col.size = new Vector3(1.2f, 0.2f, 0.2f);
        } else if (looksAt == Vector3.forward || looksAt == Vector3.back) {
            col.size = new Vector3(0.2f, 0.2f, 1.2f);
        }

        arrow.layer = LayerMask.NameToLayer("Mission");
        stick.layer = LayerMask.NameToLayer("Mission");
        cone.layer = LayerMask.NameToLayer("Mission");



        return arrow;
    }

    //init the drag move of the arrows
    private void Dragging(ArrowType type) {
        ResetHighlight(xgrafic);
        ResetHighlight(ygrafic);
        ResetHighlight(zgrafic);

        draggingArrow = type;
        startPos = transform.position;

        //determine plane for dragging based on arrow type
        Vector3 dragplane;
        if (type == ArrowType.X) {
            dragplane = Vector3.up;
            SetHighlight(xgrafic, Color.red);
        } else if (type == ArrowType.Y) {
            dragplane = Vector3.forward;
            SetHighlight(ygrafic, Color.green);
        } else {
            dragplane = Vector3.up;
            SetHighlight(zgrafic, Color.blue);
        }

        plane = new Plane(dragplane, transform.position);

        //init drag in mission editor
        if (missioneditor != null) {
            missioneditor.InitDragArr(wp.GetComponent<WaypointSelect>());
        }
    }

    //updates the position of arrows and the waypoint
    private void UpdateDragging() {
        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        float dist;

        //move arrow when plane is hit
        if (plane.Raycast(r, out dist)) {
            Vector3 hitplace = r.GetPoint(dist);
            Vector3 diff = hitplace - startPos;

            //move arrow and waypoint
            Vector3 newPos = wp.position;
            if (draggingArrow == ArrowType.X) {
                newPos += new Vector3(diff.x, 0, 0);
                transform.position += new Vector3(diff.x, 0, 0);
            } else if (draggingArrow == ArrowType.Y) {
                newPos += new Vector3(0, diff.y, 0);
                transform.position += new Vector3(0, diff.y, 0);
            } else if (draggingArrow == ArrowType.Z) {
                newPos += new Vector3(0, 0, diff.z);
                transform.position += new Vector3(0, 0, diff.z);
            }

            missioneditor.MoveWaypoint(wp.GetComponent<WaypointSelect>(), newPos);
            startPos = hitplace;
        }
    }

    private void LateUpdate() {
        if (cam == null)
            return;

        //update size with changing camera distance
        float distance = Vector3.Distance(cam.transform.position, transform.position);
        float newScale = distance * screensize;
        transform.localScale = Vector3.one * newScale;
    }

    //resets glow effect
    private void ResetHighlight(Renderer[] grafics) {
        foreach (Renderer r in grafics) {
            Material mat = r.material;
            mat.DisableKeyword("_EMISSION");
        }
    }

    //sets up glow effect
    private void SetHighlight(Renderer[] grafics, Color color) {
        foreach (Renderer g in grafics) {
            Material mat = g.material;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 30f);
        }
    }
}
