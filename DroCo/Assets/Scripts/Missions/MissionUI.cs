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
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update() {
        if (hasMission && missionController.IsMissionRunning()) {
            startMissionButtonText.text = "STOP";
            startMissionButtonImage.color = Color.red;
        } else if (hasMission) {
            startMissionButtonText.text = "START";
            startMissionButtonImage.color = Color.green;
        }
    }

    private void Start() {
        if (delete != null)
            delete.onClick.AddListener(DeleteMission);
        if (save != null)
            save.onClick.AddListener(SaveMission);
        if (missionName != null) {
            missionName.onValueChanged.AddListener(MissionNameChanged);
            missionName.text = "NewMission";
            missionNameTop.text = "NewMission";
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

    public bool HasMission() {
        return hasMission;
    }

    public void SetNewMission(GameObject building, List<Vector3> footprint, string name = null, float? osmHeight = null) {
        regenerate = false;
        missionController.SetBuilding(building, footprint);
        hasMission = true;
        missionImage.sprite = missionON;
        startMissionButton.enabled = true;
        startMissionButton.interactable = true;

        currentMissionName = name ?? "NewMission";
        if (missionName != null)
            missionName.text = currentMissionName;
        if (missionNameTop != null)
            missionNameTop.text = currentMissionName;
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
        if (buildingHeight != null)
            buildingHeight.text = osmHeight?.ToString("F1");
        RefreshList();
        regenerate = true;
    }

    private void DeleteMission() {
        if (!hasMission)
            return;

        missionController.ClearPath();
        fetcher?.ClearSelection();

        currBuilding = null;
        currFootprint = null;
        hasMission = false;
        currentMissionName = "";
        missionImage.sprite = missionOFF;
        content.gameObject.SetActive(false);
        missionController.SetSnakeMode(false);
        startMissionButton.enabled = false;
        startMissionButton.interactable = false;

        //simpleaccordion doesnt work
        //we had to force update
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content.parent as RectTransform);
        if (content.parent.parent != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content.parent.parent as RectTransform);
        }

        Toast.call.Show("Mission deleted", 2.0f, false);
    }
    private void SaveMission() {
        if (!hasMission)
            return;

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
        currentMissionName = value;
        if (missionNameTop != null)
            missionNameTop.text = currentMissionName;
    }

    private void ScanDistChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float dist) && dist > 0) {
            missionController.SetScanDistance(dist);
            GetCoverage();
            RefreshList();
        } else {
            Toast.call.Show("Invalid scan distance value", 2.0f, true);
        }
    }

    private void VerticalStepChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float step) && step > 0) {
            missionController.SetVerticalStep(step);
            GetCoverage();
            RefreshList();
        } else {
            Toast.call.Show("Invalid vertical step value", 2.0f, true);
        }
    }

    private void Use3DTubesChanged(bool value) {
        if (!regenerate)
            return;
        missionController.SetUse3DTubes(value);
        RefreshList();
    }

    private void ColorChanged(int index) {
        if (!regenerate)
            return;

        string[] colors = new string[] { "#EF476F", "#06D6A0", "#118AB2", "#FFD166", "#00B4D8", "#EC4899" };
        if (index < colors.Length) {
            if (ColorUtility.TryParseHtmlString(colors[index], out Color col)) {
                missionController.SetPathColor(col);
                color.GetComponent<Image>().color = col;
                RefreshList();
            }
        }
    }

    private void WaypointSizeChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float size) && size > 0) {
            missionController.SetWaypointSize(size);
            RefreshList();
        } else
            Toast.call.Show("Invalid waypoint size value", 2.0f, true);
    }

    private void FlightSpeedChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float speed) && speed > 0) {
            missionController.SetFlightSpeed(speed);
            GetCoverage();
        } else {
            Toast.call.Show("Invalid flight speed value", 2.0f, true);
        }
    }

    private void SegmentLenChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float len) && len > 0) {
            missionController.SetSegmentLen(len);
            RefreshList();
        } else {
            Toast.call.Show("Invalid segment length value", 2.0f, true);
        }
    }

    private void WidthOverlapChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float overlap)) {
            float dec = overlap / 100f;
            if (dec <= 0.95 && dec >= 0.1) {
                missionController.SetWidthOverlap(dec);
                GetCoverage();
                RefreshList();
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
            float dec = overlap / 100f;
            if (dec <= 0.95 && dec >= 0.1) {
                missionController.SetHeightOverlap(dec);
                GetCoverage();
                RefreshList();
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

        missionController.SetMissionType(index);
        GetCoverage();
        RefreshList();
    }

    private void BuildingHeightChanged(string value) {
        if (!regenerate)
            return;
        if (float.TryParse(value, out float height) && height > 0) {
            fetcher.RegenerateBuilding(height);
            RefreshList();
            Toast.call.Show("Building height updated, expect collisions", 3.0f, true);
        } else {
            Toast.call.Show("Invalid building height value", 2.0f, true);
        }
    }

    private void RefreshList() {
        if (waypointContent == null || waypointUIPrefab == null)
            return;

        foreach (Transform child in waypointContent) {
            Destroy(child.gameObject);
        }

        foreach (GameObject wp in missionController.GetMissionWaypoints()) {
            if (wp == null)
                continue;

            GameObject wpObj = Instantiate(waypointUIPrefab, waypointContent);
            wpObj.transform.localScale = Vector3.one;

            TMP_Text label = wpObj.GetComponentInChildren<TMP_Text>();
            if (label != null) {
                label.text = wp.name;
            }

            Button btn = wpObj.GetComponent<Button>();
            if (btn != null) {
                GameObject clickedWP = wp;
                btn.onClick.AddListener(() => SelectedUIWaypoint(clickedWP));
            }
        }

        if (waypointContent.gameObject.activeInHierarchy) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(waypointContent.GetComponent<RectTransform>());
        }
        GetCoverage();
    }

    public void DisplayAllMissions() {
        List<string> missions = missionController.GetAllMissions();
        foreach (Transform child in missionContent) {
            Destroy(child.gameObject);
        }

        foreach (string path in missions) {
            string fileName = Path.GetFileNameWithoutExtension(path);
            GameObject missionObj = Instantiate(waypointUIPrefab, missionContent);
            missionObj.transform.localScale = Vector3.one;

            TMP_Text label = missionObj.GetComponentInChildren<TMP_Text>();
            if (label != null) {
                label.text = fileName;
            }

            Button btn = missionObj.GetComponent<Button>();
            if (btn != null) {
                string clickedPath = path;
                btn.onClick.AddListener(() => SelectMission(clickedPath, missionObj));
            }
        }
    }

    public void LoadSelectedMission() {
        if (string.IsNullOrEmpty(selectedMissionPath)) {
            Toast.call.Show("No mission selected", 2.0f, true);
            return;
        }

        if (missionController.LoadMission(selectedMissionPath)) {
            GameObject building = missionController.GetCurrentBuilding();
            BoxCollider col = building.GetComponent<BoxCollider>();
            Bounds locBounds = new Bounds(col.center, col.size);
            if (building != null) {
                fetcher.FloorBoundsSelect(building, locBounds);
                fetcher.SetCurrentSelection(building);
            }
            //Toast.call.Show("Failed to load mission", 2.0f, true);
        } else {
            Toast.call.Show("Failed to load mission", 2.0f, true);
        }
    }

    public void DeleteSelectedMission() {
        if (string.IsNullOrEmpty(selectedMissionPath)) {
            Toast.call.Show("No mission selected", 2.0f, true);
            return;
        }

        if (missionController.DeleteMission(selectedMissionPath)) {
            DisplayAllMissions();
        }
    }

    private void SelectMission(string path, GameObject obj) {
        if (selectedMission != null)
            selectedMission.GetComponent<Image>().color = ColorUtility.TryParseHtmlString("#2A2A2A", out Color col) ? col : Color.white;

        selectedMissionPath = path;
        selectedMission = obj;
        obj.GetComponent<Image>().color = new Color(0.5f, 0.8f, 1f);
    }

    public void SelectedUIWaypoint(GameObject wp) {
        if (Camera.main != null) {
            Vector3 off = new Vector3(0, 5, -10);
            if (wp.name.StartsWith("WP_") && int.TryParse(wp.name.Substring(3), out int index)) {
                Vector3 look = missionController.GetNormal(index);
                if (look != Vector3.zero)
                    off = look * 3f + Vector3.up * 2f;
            }
            Camera.main.transform.position = wp.transform.position + off;
            Camera.main.transform.LookAt(wp.transform);
        }
        StartCoroutine(ShineWp(wp));
    }

    public void ShineWp(int index, Color color = default) {
        List<GameObject> wps = generator.GetMissionWaypoints();
        if (index < wps.Count && wps[index] != null)
            StartCoroutine(ShineWp(wps[index], color));
    }

    private IEnumerator ShineWp(GameObject wp, Color color = default) {
        Renderer renderer = wp.GetComponent<Renderer>();
        if (renderer == null) {
            Debug.LogWarning("Waypoint has no Renderer");
            yield break;
        }
        Material mat = renderer.material;
        Color origColor = mat.color;
        mat.EnableKeyword("_EMISSION"); //shiner
        float time = 2f;
        float done = 0f;

        while (done < time) {
            done += Time.deltaTime;
            float t = Mathf.PingPong(done * 2f, 1f);
            Color em = color * (t * 10f);
            mat.SetColor("_EmissionColor", em);
            yield return null;
        }
        mat.color = origColor;
        mat.DisableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
    }

    private void GetCoverage() {
        if (coverage != null) {
            float covW = missionController.GetWidthCoverage();
            float covH = missionController.GetHeightCoverage();
            string covWtext = GetCoverageText(covW);
            string covHtext = GetCoverageText(covH);

            coverage.text = $"W: {covWtext} H: {covHtext}";
        }
    }

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

    private void MissionStart() {
        if (hasMission && missionController.IsMissionRunning()) {
            missionController.MissionStop();
            return;
        }

        if (WebSocketServer.Instance != null && WebSocketServer.Instance.IsRunning()) {
            missionController.MissionStart();
        } else {
            Toast.call.Show("Server not running, turn on server mode and connect clients", 3.0f);
        }
    }

    public void RefreshListUI() {
        RefreshList();
    }
}