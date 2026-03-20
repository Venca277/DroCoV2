using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Collections;

public class MissionUI : MonoBehaviour {
    public static MissionUI Instance {
        get; private set;
    }

    [Header("Classes")]
    public MissionGenerator generator;
    //public DroneMissionController droneMission;
    public MissionController missionController;
    public BuildingFetcher fetcher;
    public Navigator navigator;

    [Header("UI")]
    public GameObject missionPanel;
    public Transform content;
    public Image missionImage;
    public Button startMissionButton;
    public Transform waypointContent;
    public GameObject waypointUIPrefab;

    [Header("Parameters")]
    public TMP_InputField missionName;
    public TMP_InputField scanDist;
    public TMP_InputField verticalStep;
    public TMP_InputField segmentLen;
    public TMP_InputField waypointSize;
    public TMP_InputField flightSpeed;
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
    private List<Vector3> currFootprint;
    private bool hasMission = false;
    private string currentMissionName = "";

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            Destroy(this);
        }
        Instance = this;
    }

    private void Start() {
        //InitializeUI
        if (delete != null)
            delete.onClick.AddListener(DeleteMission);
        if (save != null)
            save.onClick.AddListener(SaveMission);
        if (missionName != null) {
            missionName.onValueChanged.AddListener(MissionNameChanged);
            missionName.text = "NewMission";
        }
        if (scanDist != null)
            scanDist.onValueChanged.AddListener(ScanDistChanged);
        if (verticalStep != null)
            verticalStep.onValueChanged.AddListener(VerticalStepChanged);
        if (use3Dtubes != null)
            use3Dtubes.onValueChanged.AddListener(Use3DTubesChanged);
        if (showGhost != null)
            showGhost.onValueChanged.AddListener(showGhostChanged);
        if (color != null)
            color.onValueChanged.AddListener(ColorChanged);
        if (waypointSize != null)
            waypointSize.onValueChanged.AddListener(WaypointSizeChanged);
        if (flightSpeed != null)
            flightSpeed.onValueChanged.AddListener(FlightSpeedChanged);
        if (segmentLen != null)
            segmentLen.onValueChanged.AddListener(SegmentLenChanged);
        if (startMissionButton != null)
            startMissionButton.onClick.AddListener(MissionStart);
    }

    public bool HasMission() {
        return hasMission;
    }

    public void SetNewMission(GameObject building, List<Vector3> footprint) {
        missionController.SetBuilding(building, footprint);
        hasMission = true;
        missionImage.sprite = missionON;
        startMissionButton.enabled = true;
        startMissionButton.interactable = true;


        if (missionName != null)
            missionName.text = "NewMission";
        if (scanDist != null)
            scanDist.text = generator.scanDistance.ToString();
        if (verticalStep != null)
            verticalStep.text = generator.verticalStep.ToString();
        if (use3Dtubes != null)
            use3Dtubes.isOn = generator.use3DTubes;
        if (color != null)
            SetColor();
        if (segmentLen != null)
            segmentLen.text = generator.maxSegmentLen.ToString();
        if (waypointSize != null)
            waypointSize.text = generator.waypointSize.ToString();
        if (flightSpeed != null)
            flightSpeed.text = generator.flightSpeed.ToString();
        if (coverage != null)
            getCoverage();
        RefreshList();
    }

    private void SetColor() {
        if (color == null)
            return;

        color.options.Clear();
        color.options.Add(new TMP_Dropdown.OptionData("Red"));
        color.options.Add(new TMP_Dropdown.OptionData("Green"));
        color.options.Add(new TMP_Dropdown.OptionData("Blue"));
        color.options.Add(new TMP_Dropdown.OptionData("Yellow"));
        color.options.Add(new TMP_Dropdown.OptionData("Cyan"));
        color.options.Add(new TMP_Dropdown.OptionData("Magenta"));
        color.value = 2;
        color.RefreshShownValue();
    }

    private void DeleteMission() {
        if (!hasMission)
            return;

        generator?.ClearPath();
        //droneMission?.ClearSelection();
        fetcher?.ClearSelection();

        currBuilding = null;
        currFootprint = null;
        hasMission = false;
        currentMissionName = "";
        missionImage.sprite = missionOFF;
        content.gameObject.SetActive(false);
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

    private void RegenerateMission() {
        if (!hasMission)
            return;

        if (!missionController.Regenerate()) {
            Toast.call.Show("Regeneration not available for loaded missions", 2.0f, true);
            return;
        }

        Debug.Log("Mission regenerated");
        RefreshList();
    }

    private void MissionNameChanged(string value) {
        if (string.IsNullOrEmpty(value)) {
            Toast.call.Show("Mission name cannot be empty", 2.0f, true);
            return;
        }
        currentMissionName = value;
    }

    private void ScanDistChanged(string value) {
        if (float.TryParse(value, out float dist) && dist > 0) {
            missionController.SetScanDistance(dist);
            getCoverage();
            RefreshList();
        } else {
            Toast.call.Show("Invalid scan distance value", 2.0f, true);
        }
    }

    private void VerticalStepChanged(string value) {
        if (float.TryParse(value, out float step) && step > 0) {
            missionController.SetVerticalStep(step);
            getCoverage();
            RefreshList();
        } else {
            Toast.call.Show("Invalid vertical step value", 2.0f, true);
        }
    }

    private void Use3DTubesChanged(bool value) {
        missionController.SetUse3DTubes(value);
        RefreshList();
    }

    private void ColorChanged(int index) {
        Color[] colors = new Color[] { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta };
        if (index < colors.Length) {
            missionController.SetPathColor(colors[index]);
            RefreshList();
        }
    }

    private void showGhostChanged(bool value) {
        fetcher.showGhost = value;
        //generator.showGhost = value;
    }

    private void WaypointSizeChanged(string value) {
        if (float.TryParse(value, out float size) && size > 0) {
            missionController.SetWaypointSize(size);
            RefreshList();
        } else
            Toast.call.Show("Invalid waypoint size value", 2.0f, true);
    }

    private void FlightSpeedChanged(string value) {
        if (float.TryParse(value, out float speed) && speed > 0) {
            missionController.SetFlightSpeed(speed);
            getCoverage();
        } else {
            Toast.call.Show("Invalid flight speed value", 2.0f, true);
        }
    }

    private void SegmentLenChanged(string value) {
        if (float.TryParse(value, out float len) && len > 0) {
            missionController.SetSegmentLen(len);
            RefreshList();
        } else {
            Toast.call.Show("Invalid segment length value", 2.0f, true);
        }
    }

    private void RefreshList() {
        if (waypointContent == null || waypointUIPrefab == null)
            return;

        foreach (Transform child in waypointContent) {
            Destroy(child.gameObject);
        }

        foreach (GameObject wp in generator.GetMissionWaypoints()) {
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
                btn.onClick.AddListener(() => selectedUIWaypoint(clickedWP));
            }
        }

        if (waypointContent.gameObject.activeInHierarchy) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(waypointContent.GetComponent<RectTransform>());
        }
        getCoverage();
    }

    public void selectedUIWaypoint(GameObject wp) {
        if (Camera.main != null) {
            Camera.main.transform.position = wp.transform.position + new Vector3(0, 5, -10);
            Camera.main.transform.LookAt(wp.transform);
        }
        StartCoroutine(shineWp(wp));
    }

    private IEnumerator shineWp(GameObject wp) {
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
        Color c = new Color(0f, 1f, 1f);

        while (done < time) {
            done += Time.deltaTime;
            float t = Mathf.PingPong(done * 2f, 1f);
            Color em = c * (t * 10f);
            mat.SetColor("_EmissionColor", em);
            yield return null;
        }
        mat.color = origColor;
        mat.DisableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
    }

    private void getCoverage() {
        if (coverage != null) {
            float covW = generator.calculateWidthCoverage();
            float covH = generator.calculateHeightCoverage();
            string covWtext = getCoverageText(covW);
            string covHtext = getCoverageText(covH);

            coverage.text = $"W: {covWtext} H: {covHtext}";
        }
    }

    private string getCoverageText(float cov) {
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
        missionController.MissionStart();
    }
}