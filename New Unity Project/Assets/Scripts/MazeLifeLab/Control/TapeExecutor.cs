using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Simple tape executor that plays back recorded control segments (u, dt, N).
    /// Does not attempt to correct drift.
    /// </summary>
    public sealed class TapeExecutor : IExecutor
    {
        List<(CarControl u, float dt, int N)> tape = new List<(CarControl u, float dt, int N)>();
        Trajectory traj;
        int segIdx = 0;
        float segElapsed = 0f;
        float steerDeg = 0f;
        float motorTorque = 0f, brakeTorque = 0f;

        public float MaxMotorTorque = 1200f;
        public float MaxBrakeTorque = 2500f;
        public float AccelToTorque = 400f;
        /// <summary>If true, invert steering sign when applying to WheelColliders (useful when wheel orientation differs).</summary>
        public bool InvertSteer = false;
        /// <summary>If true, motor torque is applied to front wheels; otherwise applied to rear wheels.</summary>
        public bool FrontWheelDrive = true;
        /// <summary>Enable debug logging of applied torques/steer.</summary>
        public bool DebugLog = false;

        public bool Completed { get; private set; }
        public float LateralError => 0f;
        public float HeadingError => 0f;

        public void Load(Trajectory traj, List<(CarControl u, float dt, int N)> tape = null)
        {
            this.traj = traj;
            this.tape = tape ?? new List<(CarControl u, float dt, int N)>();
            segIdx = 0; segElapsed = 0f; Completed = false;
        }

        public void Start()
        {
            segIdx = 0; segElapsed = 0f; Completed = false;
        }

        public void Stop()
        {
            Completed = true;
        }

        public void TickFixed(float fixedDt, Transform carRoot, WheelCollider fl, WheelCollider fr, WheelCollider rl, WheelCollider rr)
        {
            if (Completed || tape == null || segIdx >= tape.Count)
            {
                // zero outputs
                if (fl != null) { fl.steerAngle = 0f; fl.motorTorque = 0f; fl.brakeTorque = 0f; }
                if (fr != null) { fr.steerAngle = 0f; fr.motorTorque = 0f; fr.brakeTorque = 0f; }
                if (rl != null) { rl.motorTorque = 0f; rl.brakeTorque = 0f; }
                if (rr != null) { rr.motorTorque = 0f; rr.brakeTorque = 0f; }
                Completed = true;
                return;
            }

            var seg = tape[segIdx];
            var u = seg.u;
            // steer in degrees for WheelCollider
            steerDeg = u.Steer * Mathf.Rad2Deg * (InvertSteer ? -1f : 1f);
            float torque = Mathf.Clamp(u.Accel * AccelToTorque, -MaxBrakeTorque, MaxMotorTorque);
            if (torque >= 0f)
            {
                motorTorque = torque; brakeTorque = 0f;
            }
            else
            {
                motorTorque = 0f; brakeTorque = Mathf.Abs(torque);
            }
            // apply steer
            if (fl != null) { fl.steerAngle = steerDeg; }
            if (fr != null) { fr.steerAngle = steerDeg; }
            // apply motor/brake depending on drive config
            if (FrontWheelDrive)
            {
                if (fl != null) { fl.motorTorque = motorTorque; fl.brakeTorque = brakeTorque; }
                if (fr != null) { fr.motorTorque = motorTorque; fr.brakeTorque = brakeTorque; }
                if (rl != null) { rl.motorTorque = 0f; rl.brakeTorque = brakeTorque; }
                if (rr != null) { rr.motorTorque = 0f; rr.brakeTorque = brakeTorque; }
            }
            else
            {
                if (rl != null) { rl.motorTorque = motorTorque; rl.brakeTorque = brakeTorque; }
                if (rr != null) { rr.motorTorque = motorTorque; rr.brakeTorque = brakeTorque; }
                if (fl != null) { fl.motorTorque = 0f; fl.brakeTorque = brakeTorque; }
                if (fr != null) { fr.motorTorque = 0f; fr.brakeTorque = brakeTorque; }
            }

            // advance by elapsed time rather than tick count so it's independent of fixedDeltaTime
            segElapsed += fixedDt;
            float segDuration = seg.dt * seg.N;
            if (segElapsed >= segDuration)
            {
                segIdx++; segElapsed = 0f;
            }

            if (DebugLog && (segIdx < tape.Count))
            {
                Debug.Log($"TapeExec seg={segIdx} steer={steerDeg:F1} motor={motorTorque:F1} brake={brakeTorque:F1} segDur={segDuration:F2}");
            }

            if (segIdx >= tape.Count) Completed = true;
        }
    }
}
