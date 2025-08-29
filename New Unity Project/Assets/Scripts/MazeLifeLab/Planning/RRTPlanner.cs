using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Kinodynamic RRT planner using piecewise-constant controls integrated with Dynamics.RK4.
    /// </summary>
    public sealed class RRTPlanner : IRRTPlanner
    {
        Dynamics dyn;
        Collision2D col;
        System.Random rng;

        /// <summary>Time step for integration (s).</summary>
        public float Dt = 0.05f;
        /// <summary>Duration of each edge (s).</summary>
        public float EdgeTime = 1.00f;
        /// <summary>Percent chance to sample the goal directly [0..100].</summary>
        public int GoalBiasPercent = 10;
        /// <summary>Maximum nodes allowed.</summary>
        public int MaxNodes = 20000;
        /// <summary>Weights for nearest metric (pos, theta, v).</summary>
        public float Wx = 1f, Wtheta = 0.5f, Wv = 0.2f;

        /// <summary>Control set explored by the planner. If null, a default grid is generated.</summary>
        public CarControl[] ControlSet;

        struct Node
        {
            public CarState s;
            public int parent;
            public CarControl u;
            public float segDt;
            public int segN;
            public List<CarState> rollout;
        }

        List<Node> nodes = new List<Node>();
        CarState start, goal;
        int goalIndex = -1;
        bool hasSolution = false;

        /// <summary>Create planner.</summary>
        public RRTPlanner(Dynamics dyn, Collision2D col, System.Random rng)
        {
            this.dyn = dyn ?? throw new ArgumentNullException(nameof(dyn));
            this.col = col ?? throw new ArgumentNullException(nameof(col));
            this.rng = rng ?? new System.Random();
            BuildDefaultControls();
        }

        void BuildDefaultControls()
        {
            if (ControlSet != null) return;
            var accs = new float[] { -2f, 0f, 1f, 2f };
            var steers = new float[] { -0.5f, -0.2f, 0f, 0.2f, 0.5f };
            var list = new List<CarControl>();
            foreach (var a in accs)
                foreach (var d in steers)
                    list.Add(new CarControl(a, d));
            ControlSet = list.ToArray();
        }

        /// <summary>Reset the planner with start and goal states.</summary>
        public void Reset(CarState start, CarState goal)
        {
            nodes.Clear();
            this.start = start; this.goal = goal;
            Node root = new Node { s = start, parent = -1, u = new CarControl(0, 0), segDt = 0f, segN = 0, rollout = new List<CarState> { start } };
            nodes.Add(root);
            goalIndex = -1; hasSolution = false;
        }

        /// <summary>Perform planning iterations. Returns true if solution found.</summary>
        public bool Step(int iterations = 1)
        {
            if (hasSolution) return true;
            int it = Math.Max(1, iterations);
            for (int k = 0; k < it; k++)
            {
                // sample state (goal biased)
                CarState xRand;
                if (rng.Next(0, 100) < GoalBiasPercent)
                {
                    xRand = goal;
                }
                else
                {
                    // simple sampling box around start and goal
                    float minx = Mathf.Min(start.X, goal.X) - 10f;
                    float maxx = Mathf.Max(start.X, goal.X) + 10f;
                    float miny = Mathf.Min(start.Y, goal.Y) - 10f;
                    float maxy = Mathf.Max(start.Y, goal.Y) + 10f;
                    float x = (float)(minx + rng.NextDouble() * (maxx - minx));
                    float y = (float)(miny + rng.NextDouble() * (maxy - miny));
                    float th = (float)((rng.NextDouble() * 2.0 - 1.0) * Math.PI);
                    float v = (float)(dyn.VMin + rng.NextDouble() * (dyn.VMax - dyn.VMin));
                    xRand = new CarState(x, y, th, v);
                }

                int nearestIdx = FindNearest(xRand);
                if (nearestIdx < 0 || nearestIdx >= nodes.Count) continue;
                var parent = nodes[nearestIdx];

                // pick random control from set
                var u = ControlSet[rng.Next(ControlSet.Length)];
                // integrate
                int segN = Mathf.Max(1, Mathf.RoundToInt(EdgeTime / Dt));
                var rollout = new List<CarState>(segN + 1);
                CarState curr = parent.s;
                rollout.Add(curr);

                bool collision = false;
                for (int i = 0; i < segN; i++)
                {
                    // enforce steer limits
                    CarControl uc = new CarControl(Mathx.Clamp(u.Accel, -dyn.AccelMax, dyn.AccelMax), Mathx.Clamp(u.Steer, -dyn.SteerMax, dyn.SteerMax));
                    curr = dyn.RK4(curr, uc, Dt);
                    rollout.Add(curr);
                    if (col != null && col.Collides(curr)) { collision = true; break; }
                    // curvature-speed check
                    float kappa = dyn.CurvatureFromSteer(curr.V, uc.Steer);
                    float vmax = dyn.SpeedLimitFromCurvature(kappa);
                    if (Mathf.Abs(curr.V) > vmax + 0.01f) { collision = true; break; }
                }

                if (collision) continue;

                Node node = new Node { s = rollout[rollout.Count - 1], parent = nearestIdx, u = u, segDt = Dt, segN = rollout.Count - 1, rollout = rollout };
                nodes.Add(node);

                // goal check (pos and heading)
                var last = node.s;
                float pdist = Mathf.Sqrt((last.X - goal.X) * (last.X - goal.X) + (last.Y - goal.Y) * (last.Y - goal.Y));
                float angErr = Mathf.Abs(Mathx.WrapAngle(last.Theta - goal.Theta));
                if (pdist < 1.0f && angErr < 0.35f)
                {
                    hasSolution = true;
                    goalIndex = nodes.Count - 1;
                    return true;
                }

                if (nodes.Count >= MaxNodes) break;
            }

            return hasSolution;
        }

        int FindNearest(CarState sample)
        {
            if (nodes.Count == 0) return -1;
            int best = 0;
            float bestCost = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                var s = nodes[i].s;
                float dx = s.X - sample.X;
                float dy = s.Y - sample.Y;
                float dpos2 = dx * dx + dy * dy;
                float dtheta = Mathx.WrapAngle(s.Theta - sample.Theta);
                float dv = s.V - sample.V;
                float cost = Wx * dpos2 + Wtheta * dtheta * dtheta + Wv * dv * dv;
                if (cost < bestCost) { bestCost = cost; best = i; }
            }
            return best;
        }

        /// <summary>Whether planner found a solution.</summary>
        public bool HasSolution => hasSolution;

        /// <summary>Return number of nodes.</summary>
        public int NodeCount => nodes.Count;

        /// <summary>Extract a time-stamped trajectory by backtracking the solution tree.
        /// The timestamps start at 0 and increment by segDt for each rollout step.
        /// </summary>
        public Trajectory ExtractTrajectory()
        {
            if (!hasSolution || goalIndex < 0) return null;
            var seq = new List<Node>();
            int idx = goalIndex;
            while (idx >= 0)
            {
                seq.Add(nodes[idx]);
                idx = nodes[idx].parent;
            }
            seq.Reverse();

            var traj = new Trajectory();
            float t = 0f;
            // include root state's first sample
            traj.Append(0f, seq[0].rollout[0]);
            for (int i = 0; i < seq.Count; i++)
            {
                var node = seq[i];
                // skip the first rollout[0] because it's already present
                int start = (i == 0) ? 1 : 0;
                for (int j = start; j < node.rollout.Count; j++)
                {
                    t += node.segDt;
                    traj.Append(t, node.rollout[j]);
                }
            }

            return traj;
        }

        /// <summary>Extract control tape corresponding to the solution.
        /// The tape contains tuples (u, dt, N) for each edge in order.
        /// </summary>
        public List<(CarControl u, float dt, int N)> ExtractTape()
        {
            if (!hasSolution || goalIndex < 0) return null;
            var tape = new List<(CarControl u, float dt, int N)>();
            int idx = goalIndex;
            var rev = new List<Node>();
            while (idx >= 0)
            {
                rev.Add(nodes[idx]);
                idx = nodes[idx].parent;
            }
            rev.Reverse();
            for (int i = 0; i < rev.Count; i++)
            {
                var n = rev[i];
                if (n.parent < 0) continue; // root
                tape.Add((n.u, n.segDt, n.segN));
            }
            return tape;
        }
    }
}
