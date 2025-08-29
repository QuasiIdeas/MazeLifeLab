using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Records time-stamped telemetry of the vehicle and planner/executor to a CSV file.
    /// Auto-starts when Play begins. Samples in FixedUpdate.
    /// </summary>
    public sealed class TelemetryRecorder : MonoBehaviour
    {
        /// <summary>Transform that represents the car root (position + orientation).</summary>
        public Transform CarRoot;
        public WheelCollider FL, FR, RL, RR;

        /// <summary>Optional manager to read planner/executor state.</summary>
        public RRTManager Manager;

        /// <summary>Directory (relative to project) where logs are written. If empty, uses Application.dataPath/../TelemetryLogs.</summary>
        public string OutputDirectory = "TelemetryLogs";

        StreamWriter writer;
        int samplesSinceFlush = 0;
        const int FlushPeriod = 64;

        void Start()
        {
            if (CarRoot == null && Manager != null) CarRoot = Manager.CarRoot;
            if (FL == null && Manager != null) { FL = Manager.FL; FR = Manager.FR; RL = Manager.RL; RR = Manager.RR; }

            // ensure directory
            string baseDir;
            try
            {
                baseDir = Path.Combine(Application.dataPath, "..", OutputDirectory);
            }
            catch
            {
                baseDir = Application.persistentDataPath;
            }
            Directory.CreateDirectory(baseDir);
            string fname = $"telemetry_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string path = Path.GetFullPath(Path.Combine(baseDir, fname));

            writer = new StreamWriter(path, false, Encoding.UTF8);
            WriteHeader();
            Debug.Log($"TelemetryRecorder: logging to {path}");
        }

        void WriteHeader()
        {
            writer.WriteLine("time,X,Y,theta_rad,V,steerFL_deg,steerFR_deg,motorFL,motorFR,motorRL,motorRR,brakeFL,brakeFR,brakeRL,brakeRR,plannerNodes,hasSolution,execCompleted,lateralError,headingError");
            writer.Flush();
        }

        void FixedUpdate()
        {
            if (writer == null) return;
            float t = Time.time;

            float x = 0f, y = 0f, th = 0f, v = 0f;
            if (CarRoot != null)
            {
                var p = CarRoot.position; x = p.x; y = p.z;
                Vector3 f = CarRoot.forward; th = Mathf.Atan2(f.z, f.x);
                var rb = CarRoot.GetComponent<Rigidbody>();
                if (rb != null) v = Vector3.Dot(rb.velocity, CarRoot.forward);
            }

            float sfl = FL != null ? FL.steerAngle : 0f;
            float sfr = FR != null ? FR.steerAngle : 0f;
            float mfl = FL != null ? FL.motorTorque : 0f;
            float mfr = FR != null ? FR.motorTorque : 0f;
            float mrl = RL != null ? RL.motorTorque : 0f;
            float mrr = RR != null ? RR.motorTorque : 0f;
            float bfl = FL != null ? FL.brakeTorque : 0f;
            float bfr = FR != null ? FR.brakeTorque : 0f;
            float brl = RL != null ? RL.brakeTorque : 0f;
            float brr = RR != null ? RR.brakeTorque : 0f;

            int nodes = 0; bool hasSol = false; bool execCompleted = false; float lat = 0f, head = 0f;
            if (Manager != null)
            {
                var pl = Manager.GetType().GetField("planner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(Manager) as IRRTPlanner;
                if (pl != null) { nodes = pl.NodeCount; hasSol = pl.HasSolution; }
                var ex = Manager.GetType().GetField("exec", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(Manager) as IExecutor;
                if (ex != null) { execCompleted = ex.Completed; lat = ex.LateralError; head = ex.HeadingError; }
            }

            // CSV line
            writer.Write(t.ToString("F3") + ",");
            writer.Write(string.Join(",", new string[] {
                x.ToString("F3"), y.ToString("F3"), th.ToString("F4"), v.ToString("F3"),
                sfl.ToString("F3"), sfr.ToString("F3"),
                mfl.ToString("F3"), mfr.ToString("F3"), mrl.ToString("F3"), mrr.ToString("F3"),
                bfl.ToString("F3"), bfr.ToString("F3"), brl.ToString("F3"), brr.ToString("F3"),
                nodes.ToString(), hasSol ? "1":"0", execCompleted ? "1":"0", lat.ToString("F3"), head.ToString("F3")
            }));
            writer.WriteLine();

            if (++samplesSinceFlush >= FlushPeriod) { writer.Flush(); samplesSinceFlush = 0; }
        }

        void OnDisable()
        {
            if (writer != null)
            {
                writer.Flush(); writer.Close(); writer = null;
                Debug.Log("TelemetryRecorder: finished logging");
            }
        }
    }
}
