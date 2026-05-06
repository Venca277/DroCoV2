// ============================================================
// MissionUI.cs
// 
// Author:  Václav Sovák
// Date:    2026-04-05
//
// 
// UI controller for mission parameters panel.
// Handles user input, parameter change and user actions such
// as saving, deleting and starting missions.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using TriLibCore.Dae.Schema;

public class MissionUI : MonoBehaviour {
    public static MissionUI Instance {
        get; private set;
    }

    [Header("Classes")]
    public MissionGenerator generator;
    public MissionController missionController;
    public BuildingFetcher fetcher;

    [Header("UI")]
    public GameObject missionPanel;
    public Transform content;
    public Image missionImage;
    public Image missionTopImage;
    public Button startMissionButton;
    public TMP_Text startMissionButtonText;
    public TMP_Text missionNameTop;
    public Image startMissionButtonImage;
    public Transform waypointContent;
    public Transform missionContent;
    public GameObject waypointUIPrefab;

    [Header("Parameters")]
    public TMP_InputField missionName;
    public TMP_Dropdown missionType;
    public TMP_InputField scanDist;
    public TMP_InputField widthOverlap;
    public TMP_InputField heightOverlap;
    public TMP_InputField verticalStep;
    public TMP_InputField segmentLen;
    public TMP_InputField waypointSize;
    public TMP_InputField flightSpeed;
    public TMP_InputField buildingHeight;
    public TMP_Dropdown color;
    public Toggle use3Dtubes;
    public Toggle showGhost;
    public TMP_Text coverage;

    [Header("Action")]
    public Button delete;
    public Button save;

    [Header("Icons")]
    public Sprite missionON;
    public Sprite missionOFF;

    private GameObject currBuilding;
    private GameObject selectedMission;
    private List<Vector3> currFootprint;
    private bool hasMission = false;
    private string currentMissionName = "";
    private string selectedMissionPath = "";
    private bool regenerate = false;

    void Awake() {
        //allow one instance only
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update() {
        //update UI on mission state change
        if (hasMission && missionController.IsMissionRunning()) {
            startMissionButtonText.text = "STOP";
            startMissionButtonImage.color = Color.red;
        } else if (hasMission) {
            startMissionButtonText.text = "START";
            startMissionButtonImage.color = Color.green;
        }
    }

    private void Start() {
        //setup listeners and initial values for dropdowns
        if (delete != null)
            delete.onClick.AddListener(DeleteMission);
        if (save != null)
            save.onClick.AddListener(SaveMission);
        if (missionName != null) {
            missionName.onValueChanged.AddListener(MissionNameChanged);
            missionName.text = "NewMission";
            missionNameTop.text = "No mission";
        }
        if (scanDist != null)
            scanDist.onEndEdit.AddListener(ScanDistChanged);
        if (verticalStep != null)
            verticalStep.onValueChanged.AddListener(VerticalStepChanged);
        if (use3Dtubes != null)
            use3Dtubes.onValueChanged.AddListener(Use3DTubesChanged);
        if (waypointSize != null)
            waypointSize.onValueChanged.AddListener(WaypointSizeChanged);
        if (flightSpeed != null)
            flightSpeed.onValueChanged.AddListener(FlightSpeedChanged);
        if (segmentLen != null)
            segmentLen.onValueChanged.AddListener(SegmentLenChanged);
        if (startMissionButton != null)
            startMissionButton.onClick.AddListener(MissionStart);
        if (widthOverlap != null)
            widthOverlap.onEndEdit.AddListener(WidthOverlapChanged);
        if (heightOverlap != null)
            heightOverlap.onEndEdit.AddListener(HeightOverlapChanged);
        if (buildingHeight != null)
            buildingHeight.onEndEdit.AddListener(BuildingHeightChanged);
        if (color != null) {
            color.options.Clear();
            color.options.Add(new TMP_Dropdown.OptionData("Red"));
            color.options.Add(new TMP_Dropdown.OptionData("Green"));
            color.options.Add(new TMP_Dropdown.OptionData("Blue"));
            color.options.Add(new TMP_Dropdown.OptionData("Yellow"));
            color.options.Add(new TMP_Dropdown.OptionData("Cyan"));
            color.options.Add(new TMP_Dropdown.OptionData("Magenta"));
            color.value = 2;
            color.RefreshShownValue();
            color.onValueChanged.AddListener(ColorChanged);
        }
        if (missionType != null) {
            missionType.options.Clear();
            missionType.options.Add(new TMP_Dropdown.OptionData("Horizontal"));
            missionType.options.Add(new TMP_Dropdown.OptionData("Vertical"));
            missionType.value = 1;
            missionType.RefreshShownValue();
            missionType.onValueChanged.AddListener(MissionTypeChanged);
        }
    }

    //set new mission to UI
    public void SetNewMission(GameObject building, List<Vector3> footprint, string name = null, float? osmHeight = null) {
        regenerate = false; //ignore triggering on value change by code                                
        missionController.SetBuilding(building, footprint);
        hasMission = true;
        //activate mission start button
        missionImage.sprite = missionON;
        startMissionButton.enabled = true;
        startMissionButton.interactable = true;

        //highlight mission name in top panel
        currentMissionName = name ?? "NewMission";
        if (missionName != null)
            missionName.text = currentMissionName;
        if (missionNameTop != null) {
            missionNameTop.text = currentMissionName;
            missionNameTop.color = new Color(0.7f, 0.97f, 0.78f);
            missionImage.sprite = missionON;
            missionTopImage.sprite = missionON;
        }
        //set text inputs to mission parameters
        if (scanDist != null)
            scanDist.text = missionController.GetScanDistance().ToString();
        if (verticalStep != null)
            verticalStep.text = missionController.GetVerticalStep().ToString();
        if (use3Dtubes != null)
            use3Dtubes.isOn = missionController.GetUse3DTubes();
        if (segmentLen != null)
            segmentLen.text = missionController.GetSegmentLen().ToString();
        if (waypointSize != null)
            waypointSize.text = missionController.GetWaypointSize().ToString();
        if (flightSpeed != null)
            flightSpeed.text = missionController.GetFlightSpeed().ToString();
        if (coverage != null)
            GetCoverage();
        if (widthOverlap != null)
            widthOverlap.text = (missionController.GetWidthOverlap() * 100f).ToString();
        if (heightOverlap != null)
            heightOverlap.text = (missionController.GetHeightOverlap() * 100f).ToString();
        if (buildingHeight != null) {
            buildingHeight.text = osmHeight?.ToString("F1");
            if (osmHeight.HasValue)
                missionController.SetCurrentBuildingHeight(osmHeight.Value);
        }

        RefreshList();
        regenerate = true; //turn on triggering on value change again
    }

    //delete current mission and reset UI
    private void DeleteMission() {
        if (!hasMission)
            return;

        missionController.ClearPath();  //clear mission trajectory
        fetcher?.ClearSelection();      //clear building selection

        currBuilding = null;
        currFootprint = null;
        hasMission = false;
        //reset UI and all its elements
        if (missionNameTop != null) {
            missionNameTop.text = "No mission";
            missionNameTop.color = Color.white;
        }
        currentMissionName = "";
        missionImage.sprite = missionOFF;
        missionTopImage.sprite = missionOFF;
        content.gameObject.SetActive(false);    //hide parameters panel
        missionController.SetSnakeMode(false);
        startMissionButton.enabled = false;
        startMissionButton.interactable = false;

        //we had to force update to recalculate sidepanel
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content.parent as RectTransform);
        if (content.parent.parent != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content.parent.parent as RectTransform);
        }

        Toast.call.Show("Mission closed", 2.0f, false);
    }
    private void SaveMission() {
        if (!hasMission)
            return;

        //check for empty mission
        string saved = missionController.SaveMission(currentMissionName);
        if (saved == null)
            Toast.call.Show("Error saving mission", 2.0f, true);
    }

    private void MissionNameChanged(string value) {
        if (!regenerate)
            return;

        if (string.IsNullOrEmpty(value)) {
            Toast.call.Show("Mission name cannot be empty", 2.0f, true);
            return;
        }

        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
            Toast.call.Show("Name contains invalid characters", 2.0f, true);
            return;
        }

        //update mission name in side and top panel
        currentMissionName = value;
        if (missionNameTop != null)
            missionNameTop.text = currentMissionName;
    }

    private void ScanDistChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float dist) && dist > 0) {
            missionController.SetScanDistance(dist);
            GetCoverage();  //recalculate image coverage
            RefreshList();  //refresh waypoint list to include new waypoints
        } else {
            Toast.call.Show("Invalid scan distance value", 2.0f, true);
        }
    }

    private void VerticalStepChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float step) && step > 0) {
            missionController.SetVerticalStep(step);
            GetCoverage();  //recalculate image coverage
            RefreshList();  //refresh waypoint list to include new waypoints
        } else {
            Toast.call.Show("Invalid vertical step value", 2.0f, true);
        }
    }

    private void Use3DTubesChanged(bool value) {
        if (!regenerate)
            return;
        //hide or show tubes
        missionController.SetUse3DTubes(value);
        RefreshList();
    }

    private void ColorChanged(int index) {
        if (!regenerate)
            return;

        //field of available colors of path
        string[] colors = new string[] { "#EF476F", "#06D6A0", "#118AB2", "#FFD166", "#00B4D8", "#EC4899" };
        if (index < colors.Length) {
            if (ColorUtility.TryParseHtmlString(colors[index], out Color col)) {
                missionController.SetPathColor(col);        //set path color
                color.GetComponent<Image>().color = col;    //update dropdown color
                RefreshList();
            }
        }
    }

    private void WaypointSizeChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float size) && size > 0 && size <= 2f) {
            missionController.SetWaypointSize(size);    //set new waypoint size
            RefreshList();
        } else
            Toast.call.Show("Invalid waypoint size value", 2.0f, true);
    }

    private void FlightSpeedChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float speed) && speed > 0) {
            //max speed of slower drones is around 13 m/s, notify user
            if (speed > 13f)
                Toast.call.Show("Maximum speed limit for smaller drones exceeded", 3.0f, true);
            missionController.SetFlightSpeed(speed); //set new flight speed
            GetCoverage();                           //recalculate image coverage
        } else {
            Toast.call.Show("Invalid flight speed value", 2.0f, true);
        }
    }

    private void SegmentLenChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float len) && len > 0) {
            missionController.SetSegmentLen(len);    //set new segment length
            RefreshList();                           //refresh waypoint list to include new waypoints
        } else {
            Toast.call.Show("Invalid segment length value", 2.0f, true);
        }
    }

    private void WidthOverlapChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float overlap)) {
            float dec = overlap / 100f;                 //convert from percentage to decimal
            if (dec <= 0.95 && dec >= 0.1) {
                missionController.SetWidthOverlap(dec); //set new width overlap
                GetCoverage();                          //recalculate image coverage
                RefreshList();                          //refresh waypoint list to include new waypoints
            } else {
                Toast.call.Show("Invalid width overlap value (10% - 95%)", 2.0f, true);
            }
        } else {
            Toast.call.Show("Invalid width overlap value (10% - 95%)", 2.0f, true);
        }
    }

    private void HeightOverlapChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float overlap)) {
            float dec = overlap / 100f;                 //convert from percentage to decimal
            if (dec <= 0.95 && dec >= 0.1) {
                missionController.SetHeightOverlap(dec); //set new height overlap
                GetCoverage();                          //recalculate image coverage
                RefreshList();                          //refresh waypoint list to include new waypoints
            } else {
                Toast.call.Show("Invalid height overlap value (10% - 95%)", 2.0f, true);
            }
        } else {
            Toast.call.Show("Invalid height overlap value (10% - 95%)", 2.0f, true);
        }
    }

    private void MissionTypeChanged(int index) {
        if (!regenerate)
            return;

        missionController.SetMissionType(index); //set new mission type
        GetCoverage();                           //recalculate image coverage
        RefreshList();                           //refresh waypoint list to include new waypoints
    }

    private void BuildingHeightChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float height) && height > 0) {
            missionController.SetCurrentBuildingHeight(height); //set new building height
            fetcher.RegenerateBuilding(height);                 //regenerate building with new height
            RefreshList();                                      //refresh waypoint list to include new waypoints
            Toast.call.Show("Building height updated, expect collisions", 3.0f, true);
        } else {
            Toast.call.Show("Invalid building height value", 2.0f, true);
        }
    }

    //refresh the list of waypoints in the UI
    private void RefreshList() {
        if (waypointContent == null || waypointUIPrefab == null)
            return;

        //clear old waypoints prefabs in UI list
        foreach (Transform child in waypointContent) {
            Destroy(child.gameObject);
        }

        //add all waypoints in scene to UI list
        foreach (GameObject wp in missionController.GetMissionWaypoints()) {
            if (wp == null)
                continue;

            //instantiate waypoint prefabs
            GameObject wpObj = Instantiate(waypointUIPrefab, waypointContent);
            wpObj.transform.localScale = Vector3.one;

            //assing waypoint name
            TMP_Text label = wpObj.GetComponentInChildren<TMP_Text>();
            if (label != null) {
                label.text = wp.name;
            }

            //add listener for focus
            Button btn = wpObj.GetComponent<Button>();
            if (btn != null) {
                GameObject clickedWP = wp;
                btn.onClick.AddListener(() => SelectedUIWaypoint(clickedWP));
            }
        }

        //force update to recalculate sidepanel
        if (waypointContent.gameObject.activeInHierarchy) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(waypointContent.GetComponent<RectTransform>());
        }
        GetCoverage();
    }

    //lists all saved missions in the UI panel
    public void DisplayAllMissions() {
        List<string> missions = missionController.GetAllMissions();
        foreach (Transform child in missionContent) {
            Destroy(child.gameObject);
        }

        //get all mission files 
        foreach (string path in missions) {
            string fileName = Path.GetFileNameWithoutExtension(path);
            GameObject missionObj = Instantiate(waypointUIPrefab, missionContent);
            missionObj.transform.localScale = Vector3.one;

            //set the files name to the prefab
            TMP_Text label = missionObj.GetComponentInChildren<TMP_Text>();
            if (label != null) {
                label.text = fileName;
            }

            //add listener for selection
            Button btn = missionObj.GetComponent<Button>();
            if (btn != null) {
                string clickedPath = path;
                btn.onClick.AddListener(() => SelectMission(clickedPath, missionObj));
            }
        }
    }

    //load the selected mission
    public void LoadSelectedMission() {
        if (string.IsNullOrEmpty(selectedMissionPath)) {
            Toast.call.Show("No mission selected", 2.0f, true);
            return;
        }

        //missioncontroller loads the selected file
        if (missionController.LoadMission(selectedMissionPath)) {
            //copy current mission objects
            GameObject building = missionController.GetCurrentBuilding();
            BoxCollider col = building.GetComponent<BoxCollider>();
            Bounds locBounds = new Bounds(col.center, col.size);
            //set building selection and floor highlight
            if (building != null) {
                fetcher.FloorBoundsSelect(building, locBounds);
                fetcher.SetCurrentSelection(building);
            }
        } else {
            Toast.call.Show("Failed to load mission", 2.0f, true);
        }
    }

    //delete the selected mission file
    public void DeleteSelectedMission() {
        if (string.IsNullOrEmpty(selectedMissionPath)) {
            Toast.call.Show("No mission selected", 2.0f, true);
            return;
        }

        //delete the selected file and refresh the list
        if (missionController.DeleteMission(selectedMissionPath)) {
            DisplayAllMissions();
        }
    }

    //selects a mission from the list and highlights it
    private void SelectMission(string path, GameObject obj) {
        //deselect previous mission, mark as visited
        if (selectedMission != null)
            selectedMission.GetComponent<Image>().color = ColorUtility.TryParseHtmlString("#2A2A2A", out Color col) ? col : Color.white;

        //select new mission, mark as selected
        selectedMissionPath = path;
        selectedMission = obj;
        obj.GetComponent<Image>().color = new Color(0.5f, 0.8f, 1f);
    }

    //selects a waypoint from the list and focuses camera
    public void SelectedUIWaypoint(GameObject wp) {
        if (Camera.main != null) {
            //focus camera on selected waypoint
            Vector3 off = new Vector3(0, 5, -10);
            if (wp.name.StartsWith("WP_") && int.TryParse(wp.name.Substring(3), out int index)) {
                //aquire normal for camera direction
                Vector3 look = missionController.GetNormal(index);
                if (look != Vector3.zero)
                    off = look * 3f + Vector3.up * 2f;
            }
            //set camera direction
            Camera.main.transform.position = wp.transform.position + off;
            Camera.main.transform.LookAt(wp.transform);
        }
        StartCoroutine(ShineWp(wp));
    }

    //shine waypoint with index
    public void ShineWp(int index, Color color = default) {
        List<GameObject> wps = generator.GetMissionWaypoints();
        if (index < wps.Count && wps[index] != null)
            StartCoroutine(ShineWp(wps[index], color)); //parallel coroutine of shining
    }

    //shines waypoint with changing its emission color
    private IEnumerator ShineWp(GameObject wp, Color color = default) {
        Renderer renderer = wp.GetComponent<Renderer>();
        if (renderer == null) {
            Debug.LogWarning("Waypoint has no Renderer");
            yield break;
        }
        Material mat = renderer.material;
        Color origColor = mat.color;        //save original color
        mat.EnableKeyword("_EMISSION");     //enable color emission
        float time = 2f;                    //2 seconds of shining
        float done = 0f;

        while (done < time) {
            //increasing and decreasing emission color intensity to max and to min
            done += Time.deltaTime;
            float t = Mathf.PingPong(done * 2f, 1f);
            Color em = color * (t * 10f);   //apply current emission color
            mat.SetColor("_EmissionColor", em);
            yield return null;
        }
        mat.color = origColor;                       //restore original color
        mat.DisableKeyword("_EMISSION");             //disable color emission
        mat.SetColor("_EmissionColor", Color.black); //reset emission color
    }

    //recalculate and update coverage text in the UI
    private void GetCoverage() {
        if (coverage != null) {
            float covW = missionController.GetWidthCoverage();
            float covH = missionController.GetHeightCoverage();
            string covWtext = GetCoverageText(covW);
            string covHtext = GetCoverageText(covH);

            //set coverage text
            coverage.text = $"W: {covWtext} H: {covHtext}";
        }
    }

    //color code coverage text based on value
    private string GetCoverageText(float cov) {
        if (cov < 0f)
            return $"<color=red>{cov:F1}%</color>";
        else if (cov < 50f)
            return $"<color=orange>{cov:F1}%</color>";
        else if (cov < 75f)
            return $"<color=yellow>{cov:F1}%</color>";
        else
            return $"<color=green>{cov:F1}%</color>";
    }

    //start or stop the mission based on current state
    private void MissionStart() {
        //if mission is running its stopped
        if (hasMission && missionController.IsMissionRunning()) {
            missionController.MissionStop();
            return;
        }

        //check server state and start mission
        if (WebSocketServer.Instance != null && WebSocketServer.Instance.IsRunning()) {
            missionController.MissionStart();
        } else {
            Toast.call.Show("Server not running, turn on server mode and connect clients", 3.0f);
        }
    }

    public void RefreshListUI() {
        RefreshList();
    }

    public bool HasMission() {
        return hasMission;
    }
}