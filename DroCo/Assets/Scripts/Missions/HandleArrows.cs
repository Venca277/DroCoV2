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

    private Camera cam;
    private ArrowType? draggingArrow;
    private Vector3 startPos;
    private Plane plane;
    public Transform wp;
    public MissionEditor missioneditor;
    public bool dragging => draggingArrow != null;

    void Start() {
        cam = Camera.main;
        xArrow = createArrow(Vector3.right, Color.red);
        yArrow = createArrow(Vector3.up, Color.green);
        zArrow = createArrow(Vector3.forward, Color.blue);
    }

    void Update() {

        //decide which arrow we drag
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

        if (Input.GetMouseButton(0) && draggingArrow != null) {
            UpdateDragging();
        }

        if (Input.GetMouseButtonUp(0)) {
            draggingArrow = null;
        }
    }

    private GameObject createArrow(Vector3 looksAt, Color color) {
        //plain game object for the arrows
        GameObject arrow = new GameObject("Arrow");
        arrow.transform.parent = transform;
        arrow.transform.localPosition = Vector3.zero;

        //stick of the arrow
        GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stick.transform.parent = arrow.transform;
        stick.transform.localPosition = looksAt * 0.5f;
        stick.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);

        //tip of the arrow
        GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cone.AddComponent<MeshFilter>();
        MeshRenderer mr = cone.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();
        cone.GetComponent<MeshFilter>().mesh = mesh;

        //size and dimension of cone
        cone.transform.parent = arrow.transform;
        cone.transform.localPosition = looksAt * 1f;
        cone.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

        //detail of the cone, segments
        int segments = 7;
        float angle = 0.0f;
        float angleAmout = 2 * Mathf.PI / segments;
        List<Vector3> verts = new List<Vector3>();
        Vector3 pos = Vector3.zero;

        //top of the cone
        pos.x = 0.0f;
        pos.y = 1f;
        pos.z = 0.0f;
        verts.Add(new Vector3(pos.x, pos.y, pos.z));

        //base of the cone
        pos.y = 0.0f;
        verts.Add(new Vector3(pos.x, pos.y, pos.z));

        //verts of the cone
        for (int i = 0; i < segments; i++) {
            pos.x = 0.5f * Mathf.Sin(angle);
            pos.z = 0.5f * Mathf.Cos(angle);

            verts.Add(new Vector3(pos.x, pos.y, pos.z));
            angle -= angleAmout;
        }

        //roteate cone to look at direction
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
            //cone.transform.localRotation = Quaternion.Euler(0, 0, 90);
            stick.transform.localRotation = Quaternion.Euler(0, 0, 90);
        } else if (looksAt == Vector3.forward || looksAt == Vector3.back) {
            //cone.transform.localRotation = Quaternion.Euler(90, 0, 0);
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

    private void Dragging(ArrowType type) {
        draggingArrow = type;
        startPos = transform.position;

        //on what plane to drag on
        Vector3 plane;
        if (type == ArrowType.X)
            plane = Vector3.up;
        else if (type == ArrowType.Y)
            plane = Vector3.forward;
        else
            plane = Vector3.up;

        this.plane = new Plane(plane, transform.position);

        //init drag in mission editor
        if (missioneditor != null) {
            missioneditor.InitDragArr(wp.GetComponent<WaypointSelect>());
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
            if (draggingArrow == ArrowType.X) {
                transform.position += new Vector3(diff.x, 0, 0);
                wp.position += new Vector3(diff.x, 0, 0);
            } else if (draggingArrow == ArrowType.Y) {
                transform.position += new Vector3(0, diff.y, 0);
                wp.position += new Vector3(0, diff.y, 0);
            } else if (draggingArrow == ArrowType.Z) {
                transform.position += new Vector3(0, 0, diff.z);
                wp.position += new Vector3(0, 0, diff.z);
            }

            startPos = hitplace;
        }
    }
}
