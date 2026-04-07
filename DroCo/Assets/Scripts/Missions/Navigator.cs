using UnityEngine;
using System.Collections.Generic;
using WebSocketSharp;
using Newtonsoft.Json;

public class Navigator : MonoBehaviour {
    [Header("Mission Data")]
    public List<Vector3> waypoints;
    public List<Vector3> waypointNormals;
    private int currentWaypointIndex = 0;
    private bool isMissionRunning = false;
    private Vector3 center;

    [Header("Navigation Settings")]
    public float waypointReach = 0.5f; //update wp on distance
    public float maxSpeed = 1.0f; //forward
    public float maxYawSpeed = 15.0f; //turning
    public float maxAscentSpeed = 0.1f; //up and down
    public string droneID = "";

    //logging for flight analysis
    private DroneManager droneManager;
    private float logFileTimer = 0f;
    private float saveFileTimer = 0f;
    private int fileCounter = 0;
    private System.Text.StringBuilder logger = new System.Text.StringBuilder();


    //hovering drone detection
    private Vector3 lastDronePos;
    private float lastDroneYaw;
    private float posTimer = 0f;
    public float timeTakeOver = 10f; //seconds without movement = user took over
    public float waypointTime = 3f; //seconds after reaching a waypoint before stale check resumes
    private float waypointTimer = 0f;
    public bool reverseOrder = false;
    private bool rotating = false;

    private float lastCmdPitch = 0f;
    private float lastCmdRoll = 0f;
    private float lastCmdYaw = 0f;
    private float lastCmdHeight = 0f;
    private Vector3 lastPhotoPos;
    private float photoDistance;

    public void StartMission(string droneID, List<Vector3> waypoints, List<Vector3> waypointNormals = null, float photoDistance = 3f) {
        droneManager = FindObjectOfType<DroneManager>();
        this.droneID = droneID;
        this.waypoints = waypoints;
        this.photoDistance = photoDistance;
        this.waypointNormals = waypointNormals;
        rotating = false;
        lastCmdPitch = 0f;
        lastCmdRoll = 0f;
        lastCmdYaw = 0f;
        lastCmdHeight = 0f;
        lastPhotoPos = Vector3.zero;

        if (reverseOrder) {
            this.waypoints = new List<Vector3>(waypoints);
            this.waypoints.Reverse();
            if (this.waypointNormals != null) {
                this.waypointNormals = new List<Vector3>(waypointNormals);
                this.waypointNormals.Reverse();
            }
        }

        currentWaypointIndex = 0;
        isMissionRunning = true;
        posTimer = 0f;
        waypointTimer = waypointTime;
        logFileTimer = 0f;
        saveFileTimer = 0f;
        logger.Clear();
        lastDronePos = droneManager != null ? GetDroneCurrentPosition() : Vector3.zero;
        lastDroneYaw = droneManager != null ? GetDroneCurrentYaw() : 0f;
        center = centerPoint(waypoints);
        MissionUI.Instance?.ShineWp(0, Color.yellow);
        Debug.Log("WP count: " + this.waypoints.Count + " | Normal count: " + (this.waypointNormals != null ? this.waypointNormals.Count.ToString() : "null"));
    }

    void Update() {
        if (!isMissionRunning || waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count) {
            if (isMissionRunning)
                StopDrone();
            return;
        }

        NavigateToCurrentWaypoint();
    }

    private Vector3 centerPoint(List<Vector3> waypoints) {
        Vector3 sum = Vector3.zero;
        int count = waypoints.Count;
        foreach (Vector3 waypoint in waypoints) {
            sum += waypoint;
        }
        return sum / count;
    }

    private void NavigateToCurrentWaypoint() {
        Vector3 dronePos = GetDroneCurrentPosition();
        float droneYaw = GetDroneCurrentYaw();

        //detect takeover
        //TODO might be useless because drone auto disables virtual control
        if (waypointTimer > 0f) {
            waypointTimer -= Time.deltaTime;
            posTimer = 0f;
            lastDronePos = dronePos; //keep updating so timer starts fresh after grace
            lastDroneYaw = droneYaw;
        } else if (Vector3.Distance(dronePos, lastDronePos) < 0.05f && Mathf.Abs(Mathf.DeltaAngle(droneYaw, lastDroneYaw)) < 2.0f && Mathf.Abs(lastCmdPitch) < 0.05f && Mathf.Abs(lastCmdRoll) < 0.05f && Mathf.Abs(lastCmdYaw) < 0.5f && Mathf.Abs(lastCmdHeight) < 0.005f) {
            posTimer += Time.deltaTime;
            if (posTimer > timeTakeOver) {
                Debug.LogWarning("Mission stopped! Takeover");
                isMissionRunning = false;
                return;
            }
        } else {
            posTimer = 0f;
            lastDronePos = dronePos;
            lastDroneYaw = droneYaw;
        }

        if (rotating) {
            Debug.Log("Rotating at WP[" + currentWaypointIndex + "] normCount=" + (waypointNormals != null ? waypointNormals.Count.ToString() : "null"));
            if (waypointNormals != null && currentWaypointIndex < waypointNormals.Count) {
                Vector3 cornerNormal = waypointNormals[currentWaypointIndex];
                cornerNormal.y = 0f;
                cornerNormal.Normalize();
                float cornerTargetYaw = Mathf.Atan2(-cornerNormal.x, -cornerNormal.z) * Mathf.Rad2Deg;
                float cornerYawErr = Mathf.DeltaAngle(droneYaw, cornerTargetYaw);

                //logging TODO remove
                //string rotLine = $"[{System.DateTime.Now:HH:mm:ss.fff}] WP[{currentWaypointIndex}] cornerYawErr={cornerYawErr:F1} target={cornerTargetYaw:F1} drone={droneYaw:F1}";
                //System.IO.File.AppendAllText(Application.persistentDataPath + "/rotation_log.txt", rotLine + "\n");

                if (Mathf.Abs(cornerYawErr) < 5f) {
                    // rotation done, move to next waypoint
                    rotating = false;
                    currentWaypointIndex++;
                    posTimer = 0f;
                    lastDronePos = dronePos;
                    lastDroneYaw = droneYaw;
                } else {
                    // hover in place, only rotate
                    float cornerCmdYaw = Mathf.Clamp(cornerYawErr * 1.5f, -maxYawSpeed, maxYawSpeed);
                    lastCmdYaw = cornerCmdYaw;
                    lastCmdHeight = 0f;
                    SendControlCommand(0f, 0f, cornerCmdYaw, 0f, 0f);
                }
            } else {
                rotating = false;
                currentWaypointIndex++;
            }
            return;
        }

        //next waypoint to travel to
        //calculate direction vector for yaw of the drone
        Vector3 targetPos = waypoints[currentWaypointIndex];
        Vector3 dirToTargetYaw = (center - dronePos);
        dirToTargetYaw.y = 0; //but only flat movement

        float targetYaw = droneYaw;
        if (waypointNormals != null && currentWaypointIndex < waypointNormals.Count) {
            int normIndex = Mathf.Max(0, currentWaypointIndex - 1);
            Vector3 normal = waypointNormals[normIndex];
            normal.y = 0f;

            if (normal.sqrMagnitude > 0.0001f) {
                normal.Normalize();
                targetYaw = Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg;
            }
        } else {
            if (dirToTargetYaw.magnitude > 0.1f) {
                targetYaw = Mathf.Atan2(dirToTargetYaw.x, dirToTargetYaw.z) * Mathf.Rad2Deg;
            }
        }

        //get distance between us and target
        float dist = Vector3.Distance(dronePos, targetPos);
        float flatDist = Vector3.Distance(new Vector3(dronePos.x, 0, dronePos.z), new Vector3(targetPos.x, 0, targetPos.z));

        if (!rotating && Vector3.Distance(dronePos, lastPhotoPos) >= photoDistance) {

            WebSocketServer.Instance?.BroadcastToAll(JsonConvert.SerializeObject(new {
                type = "take_photo"
            }));
            string frame = droneManager?.GetCameraFrame(droneID);
            if (!string.IsNullOrEmpty(frame)) {
                byte[] jpg = System.Convert.FromBase64String(frame);
                string path = Application.persistentDataPath + $"/wp_{currentWaypointIndex}.jpg";
                System.IO.File.WriteAllBytes(path, jpg);
                Toast.call.Show($"Photo taken", 1f);
            }
            lastPhotoPos = dronePos;
        }

        //waypoint reached
        //return and go to next waypoint
        if (dist < waypointReach) {
            Debug.Log("Waypoint " + currentWaypointIndex + " reached!");
            rotating = true;
            posTimer = 0f;
            lastDronePos = dronePos;
            lastDroneYaw = droneYaw;
            waypointTimer = waypointTime; //ignore takeover
            MissionUI.Instance?.ShineWp(currentWaypointIndex + 1, Color.yellow);
            return;
        }

        //get the shortest angle
        //rotation calculation
        float yawErr = Mathf.DeltaAngle(droneYaw, targetYaw);
        float cmdYaw = Mathf.Clamp(yawErr * 1.5f, -maxYawSpeed, maxYawSpeed);

        //calculate height
        float height = targetPos.y - dronePos.y;
        float cmdHeight = Mathf.Clamp(height * 0.5f, -maxAscentSpeed, maxAscentSpeed);

        //calculate forward and right speed
        Vector3 dirToTarget = (targetPos - dronePos);
        dirToTarget.y = 0; //only flat movement

        //calculate orientation
        Vector3 targetForward = Quaternion.Euler(0, targetYaw, 0) * Vector3.forward;
        Vector3 targetRight = Quaternion.Euler(0, targetYaw, 0) * Vector3.right;

        //how much to fly forward and right to get to target
        float distanceForward = Vector3.Dot(dirToTarget, targetForward);
        float distanceRight = Vector3.Dot(dirToTarget, targetRight);

        //speed regulation
        bool yawAligned = Mathf.Abs(yawErr) < 15f;
        float cmdPitch = (flatDist > 0.5f && yawAligned) ? Mathf.Clamp(distanceForward * 0.5f, -maxSpeed, maxSpeed) : 0f;
        float cmdRoll = (flatDist > 0.5f && yawAligned) ? Mathf.Clamp(distanceRight * 0.5f, -maxSpeed, maxSpeed) : 0f;

        //logging
        logFileTimer += Time.deltaTime;
        if (logFileTimer >= 1f) {
            logFileTimer = 0f;
            string line = $"[{System.DateTime.Now:HH:mm:ss}] WP:{currentWaypointIndex} dronePos:({dronePos.x:F1},{dronePos.y:F1},{dronePos.z:F1}) targetPos:({targetPos.x:F1},{targetPos.y:F1},{targetPos.z:F1}) dist:{dist:F2}m droneYaw:{droneYaw:F1} targetYaw:{targetYaw:F1} yawErr:{yawErr:F1} | PITCH:{cmdPitch:F2} ROLL:{cmdRoll:F2} YAW:{cmdYaw:F2} THR:{cmdHeight:F2}";
            //Debug.Log(line);
            logger.AppendLine(line);
        }

        saveFileTimer += Time.deltaTime;
        if (saveFileTimer >= 5f) {
            saveFileTimer = 0f;
            System.IO.File.WriteAllText(Application.persistentDataPath + $"/flight_log_{fileCounter}.txt", logger.ToString());
            fileCounter++;
        }

        lastCmdPitch = cmdPitch;
        lastCmdRoll = cmdRoll;
        lastCmdYaw = cmdYaw;
        lastCmdHeight = cmdHeight;
        SendControlCommand(cmdPitch, cmdRoll, cmdYaw, cmdHeight, 0f);
    }

    private void SendControlCommand(float pitch, float roll, float yaw, float throttle, float gimbal) {
        ControlCommand command = new ControlCommand();
        ControlCommandData data = new ControlCommandData();
        data.pitch = pitch;
        data.roll = roll;
        data.yaw = yaw;
        data.throttle = throttle;
        data.gimbal_pitch = gimbal;
        command.data = data;
        WebSocketServer.Instance?.BroadcastToAll(JsonConvert.SerializeObject(command));
    }

    public void StopDrone() {
        SendControlCommand(0, 0, 0, 0, 0);
        isMissionRunning = false;
        Debug.Log("Mission stopped.");
        string path = Application.persistentDataPath + "/flight_log.txt";
        System.IO.File.WriteAllText(path, logger.ToString());
        Toast.call.Show("Mission stopped!", 2f, true);
        logger.Clear();
    }

    private Vector3 GetDroneCurrentPosition() {
        if (droneID.IsNullOrEmpty()) {
            return droneManager.GetFirstDronePosition();
        }
        return droneManager.Drones[droneID].transform.position;
    }
    private float GetDroneCurrentYaw() {
        Drone drone = droneID.IsNullOrEmpty() ? droneManager.GetFirstDrone() : droneManager.Drones[droneID];
        if (drone?.FlightData == null) {
            return 0f;
        }
        return (float) drone.FlightData.aircraft_orientation.yaw;
    }

    public void SetMissionRunning(bool running) {
        isMissionRunning = running;
    }

    public bool IsMissionRunning() {
        return isMissionRunning;
    }
}