using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ToolPanelControl : MonoBehaviour {
    [Header("UI Reference")]
    public RawImage stream;
    public Image streamButton;
    public Image screenshotButton;
    public Image focusButton;
    public Image recordButton;
    public Sprite streamIconON;
    public Sprite streamIconOFF;
    public Sprite streamIconStreaming;
    public Sprite screenshotOFF;
    public Sprite screenshotON;
    public Sprite focusON;
    public Sprite focusOFF;
    public Sprite recordON;
    public Sprite recordOFF;
    public TMP_Text text;

    [Header("Size")]
    public Vector2 size = new Vector2(320, 180);
    public Vector2 sizebig = new Vector2(1280, 720);

    [Header("Settings")]
    public Vector3 focusDistance = new Vector3(0, 5, -10);
    public bool isFocused = false;


    private Vector2 smallposition;

    private Texture2D streamTexture;
    private RectTransform rectTransform;
    private StreamWriter writer;
    private bool isBig = false;
    public bool isRecording = false;
    private byte[] lastjpeg;
    private float lastTime = 0f;

    void Start() {
        rectTransform = stream.GetComponent<RectTransform>();
        smallposition = rectTransform.anchoredPosition;
        Debug.LogError(Application.persistentDataPath);

        streamTexture = new Texture2D(2, 2);
        stream.texture = streamTexture;

        rectTransform.sizeDelta = size;
    }

    void Update() {
        if (Time.time - lastTime > 2f) {
            screenshotButton.sprite = screenshotOFF;
        }
    }

    public void UpdateFrame(byte[] jpegData) {
        if (jpegData != null && jpegData.Length > 0) {
            lastTime = Time.time;
            lastjpeg = jpegData;
            streamTexture.LoadImage(jpegData);
            streamButton.sprite = streamIconStreaming;
            screenshotButton.sprite = screenshotON;
        }
        if (text.enabled) {
            text.enabled = false;
            stream.color = Color.white;
        }
    }

    public void ToggleSize() {
        isBig = !isBig;

        if (isBig) {
            rectTransform.sizeDelta = sizebig;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.position = new Vector2(Screen.width / 2, Screen.height / 2);
        } else {
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = smallposition;
        }
    }

    public void ToggleFocus() {
        isFocused = !isFocused;
        Vector3 pos = DroneManager.Instance.GetFirstDronePosition();
        if (isFocused) {
            if (pos == Vector3.zero) {
                Toast.call.Show("No drone in scene");
                isFocused = false;
                return;
            }
            focusButton.sprite = focusON;
            Toast.call.Show("Camera focus ON", 1f);
        } else {
            focusButton.sprite = focusOFF;
            Toast.call.Show("Camera focus OFF", 1f);
        }
    }

    public void ToggleRecording() {
        isRecording = !isRecording;
        Vector3 pos = DroneManager.Instance.GetFirstDronePosition();
        if (isRecording) {
            if (pos == Vector3.zero) {
                Toast.call.Show("No drone in scene");
                isRecording = false;
                return;
            }
            Toast.call.Show("Recording started", 1f);
            recordButton.sprite = recordON;
            StartRecording();
        } else {
            Toast.call.Show("Recording stopped", 1f);
        }
    }

    public void TurnOff() {
        stream.enabled = !stream.enabled;
        text.enabled = !text.enabled;
        if (stream.enabled) {
            streamButton.sprite = streamIconON;
        } else {
            streamButton.sprite = streamIconOFF;
            Toast.call.Show("Stream turned off", 1f);
        }
    }

    public void Screenshot() {
        if (lastjpeg == null || screenshotButton.sprite == screenshotOFF) {
            Toast.call.Show("No stream available to screenshot");
            return;
        }

        if (lastjpeg != null && lastjpeg.Length > 0) {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = "screenshot_" + timestamp + ".jpg";
            string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
            System.IO.File.WriteAllBytes(path, lastjpeg);
            Debug.Log("Screenshot saved at: " + path);
            if (streamButton.sprite == streamIconOFF) {
                Toast.call.Show("Screenshot saved, turn stream ON", 2f);
            } else {
                Toast.call.Show("Screenshot saved!", 2f);
            }
        }
    }

    public void StartRecording() {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = "flight_recorder_" + timestamp + ".csv";
        try {
            writer = new StreamWriter(System.IO.Path.Combine(Application.persistentDataPath, filename));
            writer.WriteLine("client_id,altitude,latitude,longitude,velocity_x,velocity_y,velocity_z,battery_percentage");
            Toast.call.Show("Recording started", 1f);

        } catch (IOException e) {
            Debug.LogError("Failed to create recording file: " + e.Message);
            return;
        }
    }

    public void StopRecording() {
        if (writer != null) {
            writer.Close();
            writer = null;
            Toast.call.Show("Recording stopped", 1f);
            recordButton.sprite = recordOFF;
        }
    }

    public void RecordData(DroneFlightData data) {
        if (writer != null) {
            string line = $"{data.client_id},{data.altitude},{data.gps.latitude},{data.gps.longitude},{data.aircraft_velocity.velocity_x},{data.aircraft_velocity.velocity_y},{data.aircraft_velocity.velocity_z}";
            writer.WriteLine(line);
        }
    }

    public void LateUpdate() {
        if (isFocused) {
            Vector3 dronePosition = DroneManager.Instance.GetFirstDronePosition();
            if (dronePosition == Vector3.zero) {
                return;
            }
            Vector3 targetPosition = dronePosition + focusDistance;
            Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, targetPosition, Time.deltaTime * 2f);
            Camera.main.transform.LookAt(dronePosition);
        }
    }
}
