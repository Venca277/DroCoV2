// ============================================================
// ToolPanelControl.cs
//
// Author: Václav Sovák
// Date: 2026-05-06
//
// Controls the camera stream panel, size toggle, screenshot, 
// CSV flight recording, drone camera focus and remote video 
// recording.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

public class ToolPanelControl : MonoBehaviour {
    [Header("UI Reference")]
    public RawImage stream;
    public Image streamButton;
    public Image screenshotButton;
    public Image focusButton;
    public Image recordButton;
    public Image stopButton;
    public Image videoRecordButton;
    public Sprite streamIconON;
    public Sprite streamIconOFF;
    public Sprite streamIconStreaming;
    public Sprite screenshotOFF;
    public Sprite screenshotON;
    public Sprite focusON;
    public Sprite focusOFF;
    public Sprite recordON;
    public Sprite recordOFF;
    public Sprite stopOFF;
    public Sprite stopON;
    public Sprite videoRecordON;
    public Sprite videoRecordOFF;
    public TMP_Text text;

    [Header("Size")]
    public Vector2 size = new Vector2(320, 180);        //stream compact size
    public Vector2 sizebig = new Vector2(1280, 720);    //stream big size

    [Header("Settings")]
    public Vector3 focusDistance = new Vector3(0, 5, -10);
    public bool isFocused = false;


    private Vector2 smallposition;
    private Texture2D streamTexture;
    private RectTransform rectTransform;
    private StreamWriter writer;        //CSV file writer handle
    private bool isBig = false;
    public bool isRecording = false;
    public bool isVideoRecording = false;
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
        //reset screenshot button
        if (Time.time - lastTime > 2f) {
            screenshotButton.sprite = screenshotOFF;
        }
    }

    //update stream image with new jpeg data
    public void UpdateFrame(byte[] jpegData) {
        if (jpegData != null && jpegData.Length > 0) {
            lastTime = Time.time;
            lastjpeg = jpegData;
            streamTexture.LoadImage(jpegData);
            streamButton.sprite = streamIconStreaming;
            screenshotButton.sprite = screenshotON;
        }
        //hide text on first frame received
        if (text.enabled) {
            text.enabled = false;
            stream.color = Color.white;
        }
    }

    //toggle between big and small stream size
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

    //toggle camera focus on drone
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

    //toggle flight recording
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
            StopRecording();
        }
    }

    //toggle stream
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

    //take a screenshot of the current stream frame and save to file
    public void Screenshot() {
        if (lastjpeg == null || screenshotButton.sprite == screenshotOFF) {
            Toast.call.Show("No stream available to screenshot");
            return;
        }

        WebSocketServer.Instance?.BroadcastToAll(JsonConvert.SerializeObject(new {
            type = "take_photo"
        }));
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

    //start recording flight data to CSV file
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

    //stop recording flight data to CSV file
    public void StopRecording() {
        if (writer != null) {
            writer.Close();
            writer = null;
            Toast.call.Show("Recording stopped", 1f);
            recordButton.sprite = recordOFF;
        }
    }

    //record a line of flight data to CSV file
    public void RecordData(DroneFlightData data) {
        if (writer != null) {
            string line = $"{data.client_id},{data.altitude},{data.gps.latitude},{data.gps.longitude},{data.aircraft_velocity.velocity_x},{data.aircraft_velocity.velocity_y},{data.aircraft_velocity.velocity_z}";
            writer.WriteLine(line);
        }
    }

    //updates the camera position to follow the drone
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

    //emergency stop for the mission
    public void emergencyStop() {
        MissionController controller = FindObjectOfType<MissionController>();
        if (controller != null) {
            controller.MissionStop();
        } else {
            Toast.call.Show("Stop requested failed!", 2f, true);
        }
    }

    //toggle video recording
    public void ToggleVideoRecording() {
        if (isVideoRecording) {
            isVideoRecording = false;
            WebSocketServer.Instance?.BroadcastToAll(JsonConvert.SerializeObject(new {
                type = "stop_recording"
            }));
            videoRecordButton.sprite = videoRecordOFF;
            Toast.call.Show("Video recording stopped", 1f);
        } else {
            isVideoRecording = true;
            WebSocketServer.Instance?.BroadcastToAll(JsonConvert.SerializeObject(new {
                type = "start_recording"
            }));
            videoRecordButton.sprite = videoRecordON;
            Toast.call.Show("Video recording started", 1f);
        }
    }
}
