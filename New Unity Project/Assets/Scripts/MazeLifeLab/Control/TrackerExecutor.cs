using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Trajectory tracker using Pure Pursuit steering and PI speed control.
    /// Applies outputs to WheelColliders.
    /// </summary>
    public sealed class TrackerExecutor : IExecutor
    {
        public float LookaheadMin = 1.5f;
        public float LookaheadK = 0.5f;
        public float KpSpeed = 300f, KiSpeed = 40f;
        public float MaxMotorTorque = 1200f, MaxBrakeTorque = 2500f;
        public float Wheelbase = 2.6f;

        Trajectory traj;
        List<(CarControl u, float dt, int N)> tape;
        int cursor = 0;
        float integ = 0f;
        float timeCursor = 0f;
        public bool Completed { get; private set; }
        float lateralError = 0f;
        float headingError = 0f;

        public float LateralError => lateralError;
        public float HeadingError => headingError;

        public void Load(Trajectory traj, List<(CarControl u, float dt, int N)> tape = null)
        {
            this.traj = traj;
            this.tape = tape;
            cursor = 0; integ = 0f; timeCursor = 0f; Completed = false;
        }

        public void Start()
        {
            cursor = 0; integ = 0f; timeCursor = 0f; Completed = false;
        }

        public void Stop()
        {
            Completed = true;
        }

        public void TickFixed(float fixedDt, Transform carRoot, WheelCollider fl, WheelCollider fr, WheelCollider rl, WheelCollider rr)
        {
            if (Completed || traj == null || traj.Count == 0)
            {
                if (fl != null) { fl.steerAngle = 0f; fl.motorTorque = 0f; fl.brakeTorque = 0f; }
                if (fr != null) { fr.steerAngle = 0f; fr.motorTorque = 0f; fr.brakeTorque = 0f; }
                if (rl != null) { rl.motorTorque = 0f; rl.brakeTorque = 0f; }
                if (rr != null) { rr.motorTorque = 0f; rr.brakeTorque = 0f; }
                Completed = true; return;
            }

            // vehicle pose from transform
            Vector3 pos = carRoot.position;
            float vx = pos.x; float vy = pos.z;
            float yawDeg = carRoot.eulerAngles.y;
            float yaw = yawDeg * Mathf.Deg2Rad;

            timeCursor += fixedDt;
            // reference state at current time
            CarState sRef = traj.SampleByTime(timeCursor);

            // lookahead distance
            float Ld = Mathf.Max(LookaheadMin, LookaheadK * Mathf.Abs(sRef.V));
            // find lookahead point by searching along trajectory
            CarState look = sRef;
            float accDist = 0f;
            int idx = traj.NearestByArc(new CarState(vx, vy, yaw, 0f));
            if (idx < 0) idx = 0;
            for (int i = idx; i < traj.S.Count - 1; i++)
            {
                var a = traj.S[i];
                var b = traj.S[i + 1];
                float dx = b.X - a.X;
                float dy = b.Y - a.Y;
                float segLen = Mathf.Sqrt(dx * dx + dy * dy);
                accDist += segLen;
                if (accDist >= Ld)
                {
                    look = b; break;
                }
            }

            // transform lookahead into vehicle frame
            float dxl = look.X - vx;
            float dyl = look.Y - vy;
            float lx = Mathf.Cos(yaw) * dxl + Mathf.Sin(yaw) * dyl; // forward
            float ly = -Mathf.Sin(yaw) * dxl + Mathf.Cos(yaw) * dyl; // lateral (left positive)

            // pure pursuit
            float alpha = 0f;
            if (Mathf.Abs(lx) > 1e-6f) alpha = Mathf.Atan2(ly, lx);
            lateralError = ly;
            headingError = Mathx.WrapAngle(yaw - sRef.Theta);

            float kappa = 0f;
            if (Ld > 1e-6f) kappa = 2f * Mathf.Sin(alpha) / Ld;
            float steer = Mathf.Atan(kappa * Wheelbase);

            // speed PI
            float vMeas = 0f;
            // attempt to read forward speed from carRoot's rigidbody if available
            var rb = carRoot.GetComponent<Rigidbody>();
            if (rb != null) vMeas = Vector3.Dot(rb.linearVelocity, carRoot.forward);
            float vRef = sRef.V;
            float err = vRef - vMeas;
            integ += err * fixedDt;
            float torqueCmd = KpSpeed * err + KiSpeed * integ;
            torqueCmd = Mathf.Clamp(torqueCmd, -MaxBrakeTorque, MaxMotorTorque);

            float motor = 0f, brake = 0f;
            if (torqueCmd >= 0f) { motor = torqueCmd; brake = 0f; }
            else { motor = 0f; brake = Mathf.Abs(torqueCmd); }

            // apply to wheels
            float steerDeg = steer * Mathf.Rad2Deg;
            if (fl != null) { fl.steerAngle = steerDeg; fl.motorTorque = motor; fl.brakeTorque = brake; }
            if (fr != null) { fr.steerAngle = steerDeg; fr.motorTorque = motor; fr.brakeTorque = brake; }
            if (rl != null) { rl.motorTorque = 0f; rl.brakeTorque = brake; }
            if (rr != null) { rr.motorTorque = 0f; rr.brakeTorque = brake; }

            // completion: near end of trajectory and low speed
            var last = traj.S[traj.S.Count - 1];
            float dxEnd = last.X - vx; float dyEnd = last.Y - vy;
            float distEnd = Mathf.Sqrt(dxEnd * dxEnd + dyEnd * dyEnd);
            if (distEnd < 0.6f && Mathf.Abs(vMeas) < 0.25f)
            {
                Completed = true;
            }
        }
    }
}
