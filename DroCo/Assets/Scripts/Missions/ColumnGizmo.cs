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
        Y, XZ_PLANE
    };

    private GameObject xzDisk;
    private GameObject yArrow;
    private Renderer[] diskGraphic;
    private Renderer[] ygrafic;

    private GameObject colUp;
    private GameObject colDown;
    private RaycastHit lowestHit;
    private WaypointSelect highestWp;

    private ArrowType? draggingArrow;
    private Vector3 startPos;
    private MissionEditor missioneditor;
    private Plane plane;
    public Transform wp;
    private List<WaypointSelect> Waypointscol = new List<WaypointSelect>();
    public bool dragging => draggingArrow != null;
    private bool created = false;

    void Start() {
        cam = Camera.main;
        missioneditor = FindObjectOfType<MissionEditor>();
    }

    void Update() {
        //decide which arrow we drag
        if (Input.GetMouseButtonDown(0)) {
            if (created) {
                created = false;
                return;
            }

            Ray r = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(r, out hit, 1000f, LayerMask.GetMask("Mission"))) {
                Transform trans = hit.transform;
                while (trans != null) {
                    if (trans.gameObject == yArrow) {
                        Dragging(ArrowType.Y);
                        break;
                    } else if (trans.gameObject == xzDisk) {
                        Dragging(ArrowType.XZ_PLANE);
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
            if (draggingArrow == ArrowType.XZ_PLANE) {
                hideCol(); //hide col after dragging
                ResetHighlight(diskGraphic);
            }
            draggingArrow = null;
        }
    }

    private GameObject createRing(float radius, float thickness, int segments = 32) {
        GameObject ringCont = new GameObject("XZ_Ring");
        ringCont.layer = LayerMask.NameToLayer("Mission");

        //ring texture
        GameObject ringVis = new GameObject("RingVis");
        ringVis.transform.SetParent(ringCont.transform, false);
        ringVis.layer = LayerMask.NameToLayer("Mission");

        MeshFilter meshF = ringVis.AddComponent<MeshFilter>();
        MeshRenderer meshR = ringVis.AddComponent<MeshRenderer>();

        Mesh ringMesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        float inRad = radius - thickness * 0.5f;
        float outRad = radius + thickness * 0.5f;

        //verts of two circles
        for (int i = 0; i <= segments; i++) {
            float radians = (float) i / segments * Mathf.PI * 2f;
            float cosAngle = Mathf.Cos(radians);
            float sinAngle = Mathf.Sin(radians);
            verts.Add(new Vector3(cosAngle * outRad, 0, sinAngle * outRad));
            verts.Add(new Vector3(cosAngle * inRad, 0, sinAngle * inRad));
        }

        //create triangles to fill the space
        for (int i = 0; i < segments; i++) {
            int currOut = i * 2;
            int currIn = i * 2 + 1;
            int nextOut = (i + 1) * 2;
            int nextIn = (i + 1) * 2 + 1;

            //first
            tris.Add(currOut);
            tris.Add(nextOut);
            tris.Add(currIn);

            //second
            tris.Add(currIn);
            tris.Add(nextOut);
            tris.Add(nextIn);
        }

        ringMesh.vertices = verts.ToArray();
        ringMesh.triangles = tris.ToArray();
        ringMesh.RecalculateNormals();

        meshF.mesh = ringMesh;

        //set color and material
        Material ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ringMaterial.color = new Color(0f, 1f, 1f, 1f);
        ringMaterial.SetInt("_Cull", 0); //both side to render

        meshR.material = ringMaterial;

        //collider for clicking
        BoxCollider clickCollider = ringCont.AddComponent<BoxCollider>();
        clickCollider.center = Vector3.zero;
        clickCollider.size = new Vector3(outRad * 2f, 0.2f, outRad * 2f);

        return ringCont;
    }

    public void createCol(WaypointSelect clickedWp) {
        DestroyCol();

        wp = clickedWp.transform;
        colUp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        colDown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        colUp.name = "ColUp";
        colDown.name = "ColDown";
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.5f, 0f);
        colUp.GetComponent<Renderer>().material = mat;
        colDown.GetComponent<Renderer>().material = mat;
        Destroy(colUp.GetComponent<Collider>());
        Destroy(colDown.GetComponent<Collider>());
        colDown.layer = LayerMask.NameToLayer("Mission");
        colUp.layer = LayerMask.NameToLayer("Mission");
        colUp.transform.localPosition = Vector3.zero;
        colUp.transform.localScale = new Vector3(0.1f, 1f, 0.06f);
        colDown.transform.localPosition = Vector3.zero;
        colDown.transform.localScale = new Vector3(0.1f, 1f, 0.06f);
        RaycastHit[] hits = Physics.RaycastAll(clickedWp.transform.position, Vector3.down, 1000f, LayerMask.GetMask("Mission", "Default"));
        RaycastHit[] hitsUp = Physics.RaycastAll(clickedWp.transform.position, Vector3.up, 1000f, LayerMask.GetMask("Mission"));

        if (hits.Length > 0) {
            lowestHit = hits[0];
            highestWp = clickedWp;
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
            colDown.transform.position = (lowestHit.point + clickedWp.transform.position) / 2;
            float dist = Vector3.Distance(clickedWp.transform.position, lowestHit.point);
            colDown.transform.localScale = new Vector3(0.1f, dist / 2, 0.06f);
            colDown.transform.up = (clickedWp.transform.position - lowestHit.point).normalized;

            //set the pipe to the sky
            colUp.transform.position = (clickedWp.transform.position + highestWp.transform.position) / 2;
            float distUp = Vector3.Distance(clickedWp.transform.position, highestWp.transform.position);
            colUp.transform.localScale = new Vector3(0.1f, distUp / 2, 0.06f);
            colUp.transform.up = (highestWp.transform.position - clickedWp.transform.position).normalized;
            Debug.Log("Created column");

            colDown.transform.parent = clickedWp.transform;
            colUp.transform.parent = clickedWp.transform;

            HandleArrows arrowsInst = FindObjectOfType<HandleArrows>();
            if (arrowsInst != null) {
                //create only up arrow
                yArrow = arrowsInst.createArrow(Vector3.up, Color.green);

                //create xz drag
                xzDisk = createRing(1.5f, 0.15f, 48);

                groundArrowsContainer = new GameObject("GroundArrows");
                groundArrowsContainer.transform.position = lowestHit.point;

                yArrow.transform.parent = groundArrowsContainer.transform;
                xzDisk.transform.parent = groundArrowsContainer.transform;

                yArrow.transform.localScale = new Vector3(2f, 2f, 2f);
                yArrow.transform.localPosition = Vector3.zero;
                xzDisk.transform.localScale = new Vector3(1f, 1f, 1f);
                xzDisk.transform.localPosition = Vector3.zero;

                ygrafic = yArrow.GetComponentsInChildren<Renderer>();
                diskGraphic = xzDisk.GetComponentsInChildren<Renderer>();

                hideCol(); //only in drag
                created = true;
            }
        }
    }

    private void showCol() {
        if (colUp != null) {
            colUp.SetActive(true);
        }
        if (colDown != null) {
            colDown.SetActive(true);
        }
    }

    private void hideCol() {
        if (colUp != null) {
            colUp.SetActive(false);
        }
        if (colDown != null) {
            colDown.SetActive(false);
        }
    }

    private void LateUpdate() {
        if (cam == null)
            return;

        if (groundArrowsContainer != null) {
            //update scale
            float camDist = Vector3.Distance(cam.transform.position, groundArrowsContainer.transform.position);
            float scale = camDist * screensize;
            groundArrowsContainer.transform.localScale = Vector3.one * scale;

            //ring follow ground
            Vector3 currXZPos = groundArrowsContainer.transform.position;
            Ray gndRay = new Ray(currXZPos + Vector3.up * 1000f, Vector3.down);
            RaycastHit[] gndHits = Physics.RaycastAll(gndRay, 2000f, LayerMask.GetMask("Default"));
            if (gndHits.Length > 0) {
                float low = float.MaxValue;
                foreach (RaycastHit hit in gndHits) {
                    if (hit.point.y < low)
                        low = hit.point.y;
                }
                groundArrowsContainer.transform.position = new Vector3(currXZPos.x, low, currXZPos.z);
            }
        }
    }

    private void Dragging(ArrowType type) {
        ResetHighlight(ygrafic);
        ResetHighlight(diskGraphic);

        draggingArrow = type;
        startPos = groundArrowsContainer.transform.position;

        //on what plane to drag on
        Vector3 planeNormal;
        if (type == ArrowType.Y) {
            planeNormal = Vector3.forward;
            SetHighlight(ygrafic, Color.green);
        } else {
            planeNormal = Vector3.up;
            SetHighlight(diskGraphic, Color.cyan);
            showCol(); //show col only in drag
        }

        this.plane = new Plane(planeNormal, groundArrowsContainer.transform.position);

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
            if (draggingArrow == ArrowType.Y) {
                offset = new Vector3(0, diff.y, 0);
            } else if (draggingArrow == ArrowType.XZ_PLANE) {
                offset = new Vector3(diff.x, 0, diff.z); //free movement in xz
            }

            //groundArrowsContainer.transform.position += offset;
            missioneditor.MoveWaypointColumn(Waypointscol, offset);
            startPos = hitplace;

            UpdateColLen();
        }
    }

    private void UpdateColLen() {
        if (colUp == null || colDown == null || wp == null || Waypointscol.Count == 0)
            return;

        //highest and lowest waypoint
        WaypointSelect lowWp = Waypointscol[0];
        WaypointSelect highWp = Waypointscol[0];

        foreach (WaypointSelect waypoint in Waypointscol) {
            if (waypoint.transform.position.y < lowWp.transform.position.y) {
                lowWp = waypoint;
            }
            if (waypoint.transform.position.y > highWp.transform.position.y) {
                highWp = waypoint;
            }
        }

        Vector3 lowPos = lowWp.transform.position;

        //downcol update
        Ray downRay = new Ray(lowPos, Vector3.down);
        RaycastHit gndHit;
        if (Physics.Raycast(downRay, out gndHit, 1000f, LayerMask.GetMask("Mission", "Default"))) {
            lowestHit = gndHit;
            colDown.transform.parent = null; //standalone obj

            Vector3 centerP = (gndHit.point + lowPos) / 2.0f;
            float colLen = Vector3.Distance(lowPos, gndHit.point);

            colDown.transform.position = centerP;
            colDown.transform.localScale = new Vector3(0.1f, colLen / 2f, 0.06f);
            colDown.transform.up = (lowPos - gndHit.point).normalized;
            colDown.transform.parent = lowWp.transform; //connect to lowest waypoint
        }

        //update upcol
        if (highWp != lowWp) {
            highestWp = highWp;
            Vector3 highPos = highWp.transform.position; //copy of pos
            colUp.transform.parent = null;

            Vector3 centerPoint = (lowPos + highPos) / 2.0f;
            float columnLength = Vector3.Distance(lowPos, highPos);

            colUp.transform.position = centerPoint;
            colUp.transform.localScale = new Vector3(0.1f, columnLength / 2f, 0.06f);
            colUp.transform.up = (highPos - lowPos).normalized;
            colUp.transform.parent = lowWp.transform; //connect to lowest waypoint
        } else {
            colUp.transform.localScale = Vector3.zero; //delete if only one wp
        }
    }

    public void DestroyCol() {
        if (colUp != null) {
            Destroy(colUp);
            colUp = null;
        }

        if (colDown != null) {
            Destroy(colDown);
            colDown = null;
        }

        if (groundArrowsContainer != null) {
            Destroy(groundArrowsContainer);
            groundArrowsContainer = null;
        }

        if (xzDisk != null) {
            Destroy(xzDisk);
            xzDisk = null;
        }

        if (yArrow != null) {
            Destroy(yArrow);
            yArrow = null;
        }

        Waypointscol.Clear();
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
            mat.SetColor("_EmissionColor", color * 10f);
        }
    }
}
