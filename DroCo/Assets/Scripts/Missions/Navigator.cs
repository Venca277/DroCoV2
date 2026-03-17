using UnityEngine;
using System.Collections.Generic;
using WebSocketSharp;

public class Navigator : MonoBehaviour {
    [Header("Mission Data")]
    public List<Vector3> waypoints;
    public List<Vector3> waypointNormals;
    private int currentWaypointIndex = 0;
    public bool isMissionRunning = false;
    private Vector3 center;

    [Header("Navigation Settings")]
    public float waypointReach = 1.0f; //update wp on distance
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
    public float timeTakeOver = 3f; //seconds without movement = user took over
    public float waypointTime = 3f; //seconds after reaching a waypoint before stale check resumes
    private float waypointTimer = 0f;

    public void StartMission(string droneID, List<Vector3> waypoints, List<Vector3> waypointNormals = null) {
        droneManager = FindObjectOfType<DroneManager>();
        this.droneID = droneID;
        this.waypoints = waypoints;
        this.waypointNormals = waypointNormals;
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
        } else if (Vector3.Distance(dronePos, lastDronePos) < 0.05f && Mathf.Abs(Mathf.DeltaAngle(droneYaw, lastDroneYaw)) < 2.0f) {
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

        //next waypoint to travel to
        //calculate direction vector for yaw of the drone
        Vector3 targetPos = waypoints[currentWaypointIndex];
        Vector3 dirToTargetYaw = (center - dronePos);
        dirToTargetYaw.y = 0; //but only flat movement

        float targetYaw = droneYaw;
        if (waypointNormals != null && currentWaypointIndex < waypointNormals.Count) {
            Vector3 normal = waypointNormals[currentWaypointIndex];
            normal.y = 0f;

            if (normal.sqrMagnitude > 0.0001f) {
                normal.Normalize();
                targetYaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
            }
        } else {
            if (dirToTargetYaw.magnitude > 0.1f) {
                targetYaw = Mathf.Atan2(dirToTargetYaw.x, dirToTargetYaw.z) * Mathf.Rad2Deg;
            }
        }

        //get distance between us and target
        float dist = Vector3.Distance(dronePos, targetPos);
        float flatDist = Vector3.Distance(new Vector3(dronePos.x, 0, dronePos.z), new Vector3(targetPos.x, 0, targetPos.z));

        //waypoint reached
        //return and go to next waypoint
        if (dist < waypointReach) {
            Debug.Log("Waypoint " + currentWaypointIndex + " reached!");
            currentWaypointIndex++;
            posTimer = 0f;
            lastDronePos = dronePos;
            lastDroneYaw = droneYaw;
            waypointTimer = waypointTime; //ignore takeover
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
        Vector3 droneForward = Quaternion.Euler(0, droneYaw, 0) * Vector3.forward;
        Vector3 droneRight = Quaternion.Euler(0, droneYaw, 0) * Vector3.right;

        //how much to fly forward and right to get to target
        float distanceForward = Vector3.Dot(dirToTarget, droneForward);
        float distanceRight = Vector3.Dot(dirToTarget, droneRight);

        //speed regulation
        float cmdPitch = (flatDist > 0.5f) ? Mathf.Clamp(distanceForward * 0.5f, -maxSpeed, maxSpeed) : 0f;
        float cmdRoll = (flatDist > 0.5f) ? Mathf.Clamp(distanceRight * 0.5f, -maxSpeed, maxSpeed) : 0f;


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

        SendControlCommand(cmdPitch, cmdRoll, cmdYaw, cmdHeight, 0f);
    }

    private void SendControlCommand(float pitch, float roll, float yaw, float throttle, float gimbal) {
        string jsonMsg = $@"{{
            ""type"":""control_command"",
            ""data"":{{
                ""pitch"":{pitch.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)},
                ""roll"":{roll.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)},
                ""yaw"":{yaw.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)},
                ""throttle"":{throttle.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)},
                ""gimbal_pitch"":{gimbal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}
            }}
        }}";

        WebSocketServer.Instance?.BroadcastToAll(jsonMsg);
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
}