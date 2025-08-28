using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Central manager to orchestrate planning and execution.
    /// Hotkeys: M toggle Manual/Auto, 1 Tape executor, 2 Tracker executor, R replan, Esc stop.
    /// </summary>
    public sealed class RRTManager : MonoBehaviour
    {
        public Transform CarRoot;
        public UnityEngine.WheelCollider FL, FR, RL, RR;
        public List<Transform> WallPointsGroups = new List<Transform>();
        public Transform GoalTarget;
        public bool UseTracker = true;

        [Header("Auto-detect")]
        public bool AutoFindMazeWalls = true;

        Dynamics dyn;
        Collision2D col;
        IRRTPlanner planner;
        IExecutor exec;

        enum State { Manual, Planning, ExecutingTape, ExecutingTrack }
        State state = State.Manual;

        CarState start, goal;
        Coroutine planLoop;
        Trajectory lastTraj;
        List<(CarControl u, float dt, int N)> lastTape;

        void Start()
        {
            dyn = new Dynamics();
            col = new Collision2D();
            BuildWallsFromGroups();
            planner = new RRTPlanner(dyn, col, new System.Random());
            exec = null;
        }

        /// <summary>
        /// Populate Collision2D.Walls from configured WallPointsGroups.
        /// Supports groups of point-transforms (polyline) and children that are Cubes with BoxCollider (each cube -> rectangle polyline).
        /// </summary>
        void BuildWallsFromGroups()
        {
            var walls = new List<Vector2[]>();
            foreach (var grp in WallPointsGroups)
            {
                if (grp == null) continue;

                // If the group looks like a polyline (children are points and none have BoxCollider), treat group children as a single polyline.
                bool childrenHaveCollider = false;
                for (int i = 0; i < grp.childCount; i++)
                {
                    if (grp.GetChild(i).GetComponent<BoxCollider>() != null) { childrenHaveCollider = true; break; }
                }

                if (grp.childCount >= 2 && !childrenHaveCollider)
                {
                    var pts = new List<Vector2>();
                    for (int i = 0; i < grp.childCount; i++)
                    {
                        var t = grp.GetChild(i);
                        pts.Add(new Vector2(t.position.x, t.position.z));
                    }
                    if (pts.Count >= 2) walls.Add(pts.ToArray());
                    continue;
                }

                // Otherwise iterate children: each child may be an individual wall (e.g. Cube with BoxCollider)
                for (int i = 0; i < grp.childCount; i++)
                {
                    var wallObj = grp.GetChild(i);
                    if (wallObj == null) continue;

                    // If this child itself has children that look like a polyline, use them.
                    if (wallObj.childCount >= 2)
                    {
                        bool childsHaveCollider = false;
                        for (int j = 0; j < wallObj.childCount; j++) if (wallObj.GetChild(j).GetComponent<BoxCollider>() != null) { childsHaveCollider = true; break; }
                        if (!childsHaveCollider)
                        {
                            var pts = new List<Vector2>();
                            for (int j = 0; j < wallObj.childCount; j++)
                            {
                                var t = wallObj.GetChild(j);
                                pts.Add(new Vector2(t.position.x, t.position.z));
                            }
                            if (pts.Count >= 2) walls.Add(pts.ToArray());
                            continue;
                        }
                    }

                    // If the child has a BoxCollider (e.g. a Cube), extract its top-down rectangle corners and add as a polyline.
                    var bc = wallObj.GetComponent<BoxCollider>();
                    if (bc != null)
                    {
                        // compute 4 corners in local space (top-down projection uses x/z)
                        Vector3 c = bc.center;
                        Vector3 s = bc.size * 0.5f;
                        var localCorners = new Vector3[4]
                        {
                            new Vector3(c.x - s.x, c.y, c.z - s.z),
                            new Vector3(c.x - s.x, c.y, c.z + s.z),
                            new Vector3(c.x + s.x, c.y, c.z + s.z),
                            new Vector3(c.x + s.x, c.y, c.z - s.z)
                        };
                        var pts = new List<Vector2>();
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 w = wallObj.TransformPoint(localCorners[k]);
                            pts.Add(new Vector2(w.x, w.z));
                        }
                        // add as polyline (rectangle)
                        if (pts.Count >= 2) walls.Add(pts.ToArray());
                        continue;
                    }
                }
            }

            col.SetWalls(walls);
        }

        void TryAutoAttachMazeWalls()
        {
            if (!AutoFindMazeWalls) return;
            if (WallPointsGroups != null && WallPointsGroups.Count > 0) return;

            var go = GameObject.Find("MazeWalls");
            if (go == null) return;
            // add to groups and rebuild
            WallPointsGroups.Add(go.transform);
            BuildWallsFromGroups();
        }

        void Update()
        {
            TryAutoAttachMazeWalls();

            if (Input.GetKeyDown(KeyCode.M))
            {
                if (state == State.Manual) BeginPlanning();
                else StopExecution();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) { UseTracker = false; if (lastTraj != null) StartExecution(); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { UseTracker = true; if (lastTraj != null) StartExecution(); }
            if (Input.GetKeyDown(KeyCode.R)) BeginPlanning();
            if (Input.GetKeyDown(KeyCode.Escape)) StopExecution();
        }

        void FixedUpdate()
        {
            if (state == State.ExecutingTape || state == State.ExecutingTrack)
            {
                if (exec != null)
                {
                    exec.TickFixed(Time.fixedDeltaTime, CarRoot, FL, FR, RL, RR);
                    if (exec.Completed)
                    {
                        state = State.Manual;
                    }
                }
            }
        }

        public void BeginPlanning()
        {
            if (CarRoot == null || GoalTarget == null) return;
            // set start from CarRoot (rear axle at transform position)
            Vector3 p = CarRoot.position; float yaw = CarRoot.eulerAngles.y * Mathf.Deg2Rad;
            var rb = CarRoot.GetComponent<Rigidbody>();
            float v = 0f; if (rb != null) v = Vector3.Dot(rb.velocity, CarRoot.forward);
            start = new CarState(p.x, p.z, yaw, v);
            Vector3 gp = GoalTarget.position; float gyaw = GoalTarget.eulerAngles.y * Mathf.Deg2Rad;
            goal = new CarState(gp.x, gp.z, gyaw, 0f);

            planner.Reset(start, goal);
            if (planLoop != null) StopCoroutine(planLoop);
            planLoop = StartCoroutine(PlannerLoop());
            state = State.Planning;
        }

        IEnumerator PlannerLoop()
        {
            var r = planner as RRTPlanner;
            int cap = (r != null) ? r.MaxNodes : 8000;
            while (!planner.HasSolution && planner.NodeCount < cap)
            {
                planner.Step(32);
                yield return null;
            }

            if (planner.HasSolution)
            {
                lastTraj = planner.ExtractTrajectory();
                lastTape = planner.ExtractTape();
                if (UseTracker) StartTrackerExecution(); else StartTapeExecution();
            }
            else
            {
                state = State.Manual;
            }
        }

        void StartExecution()
        {
            if (UseTracker) StartTrackerExecution(); else StartTapeExecution();
        }

        void StartTapeExecution()
        {
            exec = new TapeExecutor();
            exec.Load(lastTraj, lastTape);
            exec.Start();
            state = State.ExecutingTape;
        }

        void StartTrackerExecution()
        {
            var t = new TrackerExecutor();
            t.Load(lastTraj, lastTape);
            t.Start();
            exec = t;
            state = State.ExecutingTrack;
        }

        void StopExecution()
        {
            if (planLoop != null) { StopCoroutine(planLoop); planLoop = null; }
            if (exec != null) { exec.Stop(); exec = null; }
            state = State.Manual;
        }

        void OnDrawGizmos()
        {
            // draw walls
            if (col != null && col.Walls != null)
            {
                Gizmos.color = Color.white;
                foreach (var poly in col.Walls)
                {
                    for (int i = 0; i < poly.Length - 1; i++)
                    {
                        Vector3 a = new Vector3(poly[i].x, 0f, poly[i].y);
                        Vector3 b = new Vector3(poly[i + 1].x, 0f, poly[i + 1].y);
                        Gizmos.DrawLine(a, b);
                    }
                }
            }

            // draw final path
            if (lastTraj != null)
            {
                var pts = lastTraj.ToWorldPolyline();
                Gizmos.color = Color.green;
                for (int i = 0; i < pts.Length - 1; i++) Gizmos.DrawLine(pts[i], pts[i + 1]);
            }
        }
    }
}
