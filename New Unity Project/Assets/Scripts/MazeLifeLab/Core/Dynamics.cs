using System;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Kinematic bicycle dynamics with simple RK4 integrator.
    /// All angles are radians internally.
    /// </summary>
    public sealed class Dynamics
    {
        /// <summary>Wheelbase length (m).</summary>
        public float L = 2.6f;
        /// <summary>Minimum forward speed (m/s).</summary>
        public float VMin = -5f, VMax = 12f;
        /// <summary>Maximum steering angle (radians).</summary>
        public float SteerMax = 0.6f;   // rad
        /// <summary>Maximum longitudinal acceleration (m/s^2).</summary>
        public float AccelMax = 3f;     // m/s^2
        /// <summary>Maximum lateral acceleration (m/s^2) used to derive curvature-dependent speed limit.</summary>
        public float LatAccelMax = 6f;  // m/s^2 limit for curvature speed

        /// <summary>State derivative f(s,u).</summary>
        public CarState F(CarState s, CarControl u)
        {
            float steer = Mathx.Clamp(u.Steer, -SteerMax, SteerMax);
            float accel = Mathx.Clamp(u.Accel, -AccelMax, AccelMax);

            float dx = s.V * Mathf.Cos(s.Theta);
            float dy = s.V * Mathf.Sin(s.Theta);
            float dtheta = s.V * Mathf.Tan(steer) / L;
            float dv = accel;
            return new CarState(dx, dy, dtheta, dv);
        }

        /// <summary>
        /// RK4 integrator step for time dt. Theta wrapping and speed clamping applied to the result.
        /// </summary>
        public CarState RK4(CarState s, CarControl u, float dt)
        {
            // small dt -> effectively Euler
            var k1 = F(s, u);
            var s2 = new CarState(s.X + 0.5f * dt * k1.X, s.Y + 0.5f * dt * k1.Y, s.Theta + 0.5f * dt * k1.Theta, s.V + 0.5f * dt * k1.V);
            var k2 = F(s2, u);
            var s3 = new CarState(s.X + 0.5f * dt * k2.X, s.Y + 0.5f * dt * k2.Y, s.Theta + 0.5f * dt * k2.Theta, s.V + 0.5f * dt * k2.V);
            var k3 = F(s3, u);
            var s4 = new CarState(s.X + dt * k3.X, s.Y + dt * k3.Y, s.Theta + dt * k3.Theta, s.V + dt * k3.V);
            var k4 = F(s4, u);

            float nx = s.X + dt * (k1.X + 2f * k2.X + 2f * k3.X + k4.X) / 6f;
            float ny = s.Y + dt * (k1.Y + 2f * k2.Y + 2f * k3.Y + k4.Y) / 6f;
            float nth = s.Theta + dt * (k1.Theta + 2f * k2.Theta + 2f * k3.Theta + k4.Theta) / 6f;
            float nv = s.V + dt * (k1.V + 2f * k2.V + 2f * k3.V + k4.V) / 6f;

            CarState outS = new CarState(nx, ny, Mathx.WrapAngle(nth), nv);
            return ClampState(outS);
        }

        /// <summary>Clamp velocities and wrap theta.</summary>
        public CarState ClampState(CarState s)
        {
            s.Theta = Mathx.WrapAngle(s.Theta);
            s.V = Mathx.Clamp(s.V, VMin, VMax);
            return s;
        }

        /// <summary>Curvature induced by steering at speed v: tan(delta)/L.</summary>
        public float CurvatureFromSteer(float v, float steer)
        {
            float s = Mathx.Clamp(steer, -SteerMax, SteerMax);
            return Mathf.Tan(s) / L;
        }

        /// <summary>Speed limit for a given curvature based on lateral acceleration bound.</summary>
        public float SpeedLimitFromCurvature(float kappa)
        {
            if (Mathf.Approximately(kappa, 0f)) return VMax;
            return Mathf.Sqrt(Mathf.Max(0.01f, LatAccelMax / Mathf.Abs(kappa)));
        }
    }
}
