using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Minimal car state for planning: rear-axle position (X,Y), heading Theta (radians), and forward speed V (m/s).
    /// </summary>
    public struct CarState
    {
        /// <summary>X position in world XY plane.</summary>
        public float X;
        /// <summary>Y position in world XY plane.</summary>
        public float Y;
        /// <summary>Heading (radians).</summary>
        public float Theta;
        /// <summary>Forward speed (m/s).</summary>
        public float V;

        /// <summary>Construct a CarState.</summary>
        public CarState(float x, float y, float theta, float v)
        {
            X = x; Y = y; Theta = theta; V = v;
        }
    }

    /// <summary>
    /// Control pair for the car: longitudinal acceleration and steering angle (radians).
    /// </summary>
    public struct CarControl
    {
        /// <summary>Longitudinal acceleration (m/s^2).</summary>
        public float Accel;
        /// <summary>Steering angle (radians).</summary>
        public float Steer;

        /// <summary>Construct a CarControl.</summary>
        public CarControl(float accel, float steer)
        {
            Accel = accel; Steer = steer;
        }
    }

    /// <summary>
    /// Math helpers used across the project.
    /// </summary>
    public static class Mathx
    {
        /// <summary>
        /// Wrap angle into the interval (-PI, PI].
        /// </summary>
        public static float WrapAngle(float rad)
        {
            const float PI = Mathf.PI;
            const float TWO_PI = 2f * Mathf.PI;
            float a = rad;
            // bring within (-PI, PI]
            while (a <= -PI) a += TWO_PI;
            while (a > PI) a -= TWO_PI;
            return a;
        }

        /// <summary>
        /// Clamp value between min and max.
        /// </summary>
        public static float Clamp(float value, float min, float max)
        {
            return Mathf.Max(min, Mathf.Min(max, value));
        }
    }

    /// <summary>
    /// Time-indexed trajectory container for states.
    /// T and S are parallel lists: T[i] is the timestamp for state S[i].
    /// </summary>
    public sealed class Trajectory
    {
        /// <summary>Timestamps (seconds), strictly increasing.</summary>
        public List<float> T = new List<float>();
        /// <summary>States at corresponding timestamps.</summary>
        public List<CarState> S = new List<CarState>();

        /// <summary>Clear the trajectory.</summary>
        public void Clear()
        {
            T.Clear(); S.Clear();
        }

        /// <summary>Number of samples.</summary>
        public int Count => S.Count;

        /// <summary>Duration of trajectory in seconds. Zero if empty.</summary>
        public float Duration => (T.Count == 0) ? 0f : (T[T.Count - 1] - T[0]);

        /// <summary>Append a timestamped state. T must be strictly increasing.</summary>
        public void Append(float t, CarState s)
        {
            if (T.Count > 0 && t <= T[T.Count - 1])
                throw new ArgumentException("Timestamp must be strictly increasing.");
            T.Add(t);
            S.Add(s);
        }

        /// <summary>
        /// Sample the trajectory by time using linear interpolation. Time is clamped to [0, Duration].
        /// Theta is interpolated along the shortest angular direction.
        /// </summary>
        public CarState SampleByTime(float t)
        {
            if (S.Count == 0)
                throw new InvalidOperationException("Trajectory is empty.");

            float t0 = T[0];
            float tf = T[T.Count - 1];
            float tt = t;
            if (tt <= t0) return S[0];
            if (tt >= tf) return S[S.Count - 1];

            // find interval
            int i = T.BinarySearch(tt);
            if (i >= 0) return S[i];
            i = ~i;
            int i0 = Mathf.Max(0, i - 1);
            int i1 = Mathf.Min(S.Count - 1, i);
            float ta = T[i0];
            float tb = T[i1];
            float alpha = (tt - ta) / (tb - ta);

            CarState a = S[i0];
            CarState b = S[i1];

            float x = Mathf.Lerp(a.X, b.X, alpha);
            float y = Mathf.Lerp(a.Y, b.Y, alpha);
            float v = Mathf.Lerp(a.V, b.V, alpha);
            // shortest angular interpolation
            float dtheta = Mathx.WrapAngle(b.Theta - a.Theta);
            float theta = Mathx.WrapAngle(a.Theta + alpha * dtheta);

            return new CarState(x, y, theta, v);
        }

        /// <summary>
        /// Find the index of the nearest state by Euclidean distance in XY, starting the search at startIdx.
        /// </summary>
        public int NearestByArc(CarState pose, int startIdx = 0)
        {
            int best = Mathf.Clamp(startIdx, 0, S.Count - 1);
            if (S.Count == 0) return -1;
            float bestDist = float.MaxValue;
            for (int i = startIdx; i < S.Count; i++)
            {
                var s = S[i];
                float dx = s.X - pose.X;
                float dy = s.Y - pose.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestDist)
                {
                    bestDist = d2; best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// Convert trajectory XY states to a Unity world polyline (Vector3[]), mapping (X,Y) -> (x,0,z).
        /// </summary>
        public Vector3[] ToWorldPolyline()
        {
            Vector3[] pts = new Vector3[S.Count];
            for (int i = 0; i < S.Count; i++)
            {
                pts[i] = new Vector3(S[i].X, 0f, S[i].Y);
            }
            return pts;
        }
    }
}
