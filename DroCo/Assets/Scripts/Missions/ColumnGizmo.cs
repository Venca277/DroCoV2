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
    public MissionEditor missioneditor;
    private Plane plane;
    public Transform wp;
    private List<WaypointSelect> waypointsCol = new List<WaypointSelect>();
    public bool dragging => draggingArrow != null;
    private bool created = false;
    private int missionLayer;
    private LayerMask missionMask;
    private LayerMask gndMask;
    private LayerMask missionGndMask;
    private Shader shader;
    private HandleArrows handleArrows;

    void Start() {
        cam = Camera.main;
        missionLayer = LayerMask.NameToLayer("Mission");
        missionMask = LayerMask.GetMask("Mission");
        gndMask = LayerMask.GetMask("Default");
        missionGndMask = LayerMask.GetMask("Mission", "Default");
        shader = Shader.Find("Universal Render Pipeline/Lit");
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
            if (Physics.Raycast(r, out hit, 1000f, missionMask)) {
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
                ShowCol(false); //hide col after dragging
                ResetHighlight(diskGraphic);
            }
            draggingArrow = null;
        }
    }

    private GameObject CreateRing(float radius, float thickness, int segments = 32) {
        GameObject ringCont = new GameObject("XZ_Ring");
        ringCont.layer = missionLayer;

        GameObject ringVis = new GameObject("RingVis");
        ringVis.transform.SetParent(ringCont.transform, false);
        ringVis.layer = missionLayer;

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

        Material ringMaterial = new Material(shader);
        ringMaterial.color = new Color(0f, 1f, 1f, 1f);
        ringMaterial.SetInt("_Cull", 0);

        meshR.material = ringMaterial;

        BoxCollider clickCollider = ringCont.AddComponent<BoxCollider>();
        clickCollider.center = Vector3.zero;
        clickCollider.size = new Vector3(outRad * 2f, 0.2f, outRad * 2f);

        return ringCont;
    }

    public void CreateCol(WaypointSelect clickedWp) {
        DestroyCol();
        wp = clickedWp.transform;

        colUp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        colDown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        colUp.name = "ColUp";
        colDown.name = "ColDown";

        Material mat = new Material(shader);
        mat.color = new Color(1f, 0.5f, 0f);
        colUp.GetComponent<Renderer>().material = mat;
        colDown.GetComponent<Renderer>().material = mat;

        //remove colliders cols will never be clicable
        Destroy(colUp.GetComponent<Collider>());
        Destroy(colDown.GetComponent<Collider>());

        colDown.layer = missionLayer;
        colUp.layer = missionLayer;
        colUp.transform.localPosition = Vector3.zero;
        colUp.transform.localScale = new Vector3(0.1f, 1f, 0.06f);
        colDown.transform.localPosition = Vector3.zero;
        colDown.transform.localScale = new Vector3(0.1f, 1f, 0.06f);

        RaycastHit[] hits = Physics.RaycastAll(clickedWp.transform.position, Vector3.down, 1000f, missionGndMask);
        RaycastHit[] hitsUp = Physics.RaycastAll(clickedWp.transform.position, Vector3.up, 1000f, missionMask);

        if (hits.Length > 0) {
            lowestHit = hits[0];
            highestWp = clickedWp;
            float highestY = clickedWp.transform.position.y;

            waypointsCol.Clear();
            waypointsCol.Add(clickedWp);

            foreach (RaycastHit hit in hits) {
                if (lowestHit.point.y > hit.point.y) {
                    lowestHit = hit;
                }
                WaypointSelect wp = hit.transform.GetComponent<WaypointSelect>();
                if (wp != null && !waypointsCol.Contains(wp)) {
                    waypointsCol.Add(wp);
                }
            }
            foreach (RaycastHit hit in hitsUp) {
                WaypointSelect wp = hit.transform.GetComponent<WaypointSelect>();
                if (wp != null && wp != clickedWp && hit.point.y > highestY) {
                    highestY = hit.point.y;
                    highestWp = wp;
                }
                if (wp != null && !waypointsCol.Contains(wp)) {
                    waypointsCol.Add(wp);
                }
            }

            //set pipe to the ground hit
            PosCylinder(colDown, lowestHit.point, clickedWp.transform.position);

            //set the pipe to the sky
            PosCylinder(colUp, clickedWp.transform.position, highestWp.transform.position);

            colDown.transform.parent = clickedWp.transform;
            colUp.transform.parent = clickedWp.transform;

            handleArrows = FindObjectOfType<HandleArrows>();
            if (handleArrows != null) {
                //create only up arrow
                yArrow = handleArrows.CreateArrow(Vector3.up, Color.green);

                //create xz drag
                xzDisk = CreateRing(1.5f, 0.15f, 48);

                groundArrowsContainer = new GameObject("GroundArrows");
                groundArrowsContainer.transform.position = lowestHit.point;

                yArrow.transform.parent = groundArrowsContainer.transform;
                xzDisk.transform.parent = groundArrowsContainer.transform;

                yArrow.transform.localScale = new Vector3(2f, 2f, 2f);
                yArrow.transform.localPosition = Vector3.zero;
                xzDisk.transform.localScale = Vector3.one;
                xzDisk.transform.localPosition = Vector3.zero;

                ygrafic = yArrow.GetComponentsInChildren<Renderer>();
                diskGraphic = xzDisk.GetComponentsInChildren<Renderer>();

                ShowCol(false); //only in drag
                created = true;
            }
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
            RaycastHit[] gndHits = Physics.RaycastAll(gndRay, 2000f, gndMask);
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
        if (ygrafic != null)
            ResetHighlight(ygrafic);
        if (diskGraphic != null)
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
            ShowCol(true); //show col only in drag
        }

        this.plane = new Plane(planeNormal, groundArrowsContainer.transform.position);

        //init drag in mission editor
        if (missioneditor != null) {
            missioneditor.InitDragColumn(waypointsCol);
        }
    }

    private void UpdateDragging() {
        Ray r = cam.ScreenPointToRay(Input.mousePosition);

        //if ray hits plane we move the arrow
        if (plane.Raycast(r, out float dist)) {
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
            missioneditor.MoveWaypointColumn(waypointsCol, offset);
            startPos = hitplace;

            UpdateColLen();
        }
    }

    private void UpdateColLen() {
        if (colUp == null || colDown == null || wp == null || waypointsCol.Count == 0)
            return;

        //highest and lowest waypoint
        WaypointSelect lowWp = waypointsCol[0];
        WaypointSelect highWp = waypointsCol[0];

        foreach (WaypointSelect waypoint in waypointsCol) {
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
        if (Physics.Raycast(downRay, out gndHit, 1000f, missionGndMask)) {
            lowestHit = gndHit;
            colDown.transform.parent = null; //standalone obj

            PosCylinder(colDown, gndHit.point, lowPos);
            colDown.transform.parent = lowWp.transform;
        }

        //update upcol
        if (highWp != lowWp) {
            highestWp = highWp;
            Vector3 highPos = highWp.transform.position; //copy of pos
            colUp.transform.parent = null;

            PosCylinder(colUp, lowPos, highPos);
            colUp.transform.parent = lowWp.transform;
        } else {
            ShowCol(false);
            colUp.transform.localScale = Vector3.zero; //delete if only one wp
        }
    }

    private void PosCylinder(GameObject cyl, Vector3 from, Vector3 to) {
        cyl.transform.position = (from + to) / 2f;
        cyl.transform.localScale = new Vector3(0.1f, Vector3.Distance(from, to) / 2f, 0.06f);
        cyl.transform.up = (to - from).normalized;
    }

    public void DestroyCol() {
        Destroy(colUp);
        Destroy(colDown);
        Destroy(groundArrowsContainer);
        Destroy(xzDisk);
        Destroy(yArrow);

        yArrow = null;
        colUp = null;
        colDown = null;
        groundArrowsContainer = null;
        xzDisk = null;
        waypointsCol.Clear();
    }

    private void ShowCol(bool show) {
        if (colUp != null) {
            colUp.SetActive(show);
        }
        if (colDown != null) {
            colDown.SetActive(show);
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
            mat.SetColor("_EmissionColor", color * 10f);
        }
    }
}
