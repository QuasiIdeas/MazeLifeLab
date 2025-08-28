using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Fast 2D collision checks between a rectangular car footprint and static polylines (walls).
    /// All geometry in XY.
    /// </summary>
    public sealed class Collision2D
    {
        /// <summary>Car length (m).</summary>
        public float CarLength = 4.3f, CarWidth = 1.9f, Inflation = 0.15f;

        /// <summary>Wall polylines in XY (each array is a polyline of points).</summary>
        public List<Vector2[]> Walls = new List<Vector2[]>();

        /// <summary>Set walls (replaces existing).</summary>
        public void SetWalls(List<Vector2[]> walls)
        {
            Walls = walls ?? new List<Vector2[]>();
        }

        /// <summary>
        /// Check if car at state s collides with any wall segment.
        /// The rectangle is centered relative to the rear axle as specified in the requirements.
        /// </summary>
        public bool Collides(CarState s)
        {
            if (Walls == null || Walls.Count == 0) return false;

            // compute rectangle center (world XY)
            float cx = s.X + 0.5f * CarLength * Mathf.Cos(s.Theta);
            float cy = s.Y + 0.5f * CarLength * Mathf.Sin(s.Theta);
            float hx = 0.5f * CarLength + Inflation;
            float hy = 0.5f * CarWidth + Inflation;

            // rotation to local coords
            float cos = Mathf.Cos(-s.Theta);
            float sin = Mathf.Sin(-s.Theta);

            foreach (var poly in Walls)
            {
                if (poly == null || poly.Length < 2) continue;
                for (int i = 0; i < poly.Length - 1; i++)
                {
                    Vector2 a = poly[i];
                    Vector2 b = poly[i + 1];
                    // transform endpoints to local rect coords (centered at rectangle center)
                    Vector2 la = WorldToLocal(a, cx, cy, cos, sin);
                    Vector2 lb = WorldToLocal(b, cx, cy, cos, sin);

                    // if either endpoint inside rect -> collision
                    if (PointInAABB(la, hx, hy) || PointInAABB(lb, hx, hy)) return true;

                    // compute closest point on segment to origin
                    Vector2 closest = ClosestPointOnSegment(Vector2.zero, la, lb);
                    if (Mathf.Abs(closest.x) <= hx && Mathf.Abs(closest.y) <= hy) return true;
                }
            }

            return false;
        }

        /// <summary>Check a rollout (list of states). Stride controls sampling frequency.</summary>
        public bool SegmentRolloutCollides(List<CarState> rollout, int stride = 1)
        {
            if (rollout == null) return false;
            for (int i = 0; i < rollout.Count; i += Math.Max(1, stride))
            {
                if (Collides(rollout[i])) return true;
            }
            return false;
        }

        static Vector2 WorldToLocal(Vector2 p, float cx, float cy, float cos, float sin)
        {
            float dx = p.x - cx;
            float dy = p.y - cy;
            return new Vector2(dx * cos - dy * sin, dx * sin + dy * cos);
        }

        static bool PointInAABB(Vector2 p, float hx, float hy)
        {
            return Mathf.Abs(p.x) <= hx && Mathf.Abs(p.y) <= hy;
        }

        static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float ab2 = Vector2.Dot(ab, ab);
            if (ab2 == 0f) return a;
            float t = Vector2.Dot(p - a, ab) / ab2;
            t = Mathf.Clamp01(t);
            return a + t * ab;
        }

        /// <summary>Distance from point p to segment ab.</summary>
        public static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 c = ClosestPointOnSegment(p, a, b);
            return Vector2.Distance(p, c);
        }
    }
}
