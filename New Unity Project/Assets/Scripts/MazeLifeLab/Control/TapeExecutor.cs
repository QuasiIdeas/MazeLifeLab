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
        int tickInSeg = 0;
        float steerDeg = 0f;
        float motorTorque = 0f, brakeTorque = 0f;

        public float MaxMotorTorque = 1200f;
        public float MaxBrakeTorque = 2500f;
        public float AccelToTorque = 400f;

        public bool Completed { get; private set; }
        public float LateralError => 0f;
        public float HeadingError => 0f;

        public void Load(Trajectory traj, List<(CarControl u, float dt, int N)> tape = null)
        {
            this.traj = traj;
            this.tape = tape ?? new List<(CarControl u, float dt, int N)>();
            segIdx = 0; tickInSeg = 0; Completed = false;
        }

        public void Start()
        {
            segIdx = 0; tickInSeg = 0; Completed = false;
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
            steerDeg = u.Steer * Mathf.Rad2Deg;
            float torque = Mathf.Clamp(u.Accel * AccelToTorque, -MaxBrakeTorque, MaxMotorTorque);
            if (torque >= 0f)
            {
                motorTorque = torque; brakeTorque = 0f;
            }
            else
            {
                motorTorque = 0f; brakeTorque = Mathf.Abs(torque);
            }

            if (fl != null) { fl.steerAngle = steerDeg; fl.motorTorque = motorTorque; fl.brakeTorque = brakeTorque; }
            if (fr != null) { fr.steerAngle = steerDeg; fr.motorTorque = motorTorque; fr.brakeTorque = brakeTorque; }
            if (rl != null) { rl.motorTorque = 0f; rl.brakeTorque = brakeTorque; }
            if (rr != null) { rr.motorTorque = 0f; rr.brakeTorque = brakeTorque; }

            tickInSeg++;
            if (tickInSeg >= seg.N)
            {
                segIdx++; tickInSeg = 0;
            }

            if (segIdx >= tape.Count) Completed = true;
        }
    }
}
