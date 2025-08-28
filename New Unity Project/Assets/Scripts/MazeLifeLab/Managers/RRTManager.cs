using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Central manager to orchestrate planning and execution.
    /// Hotkeys: M toggle Manual/Auto, 1 Tape executor, 2 Tracker executor, R replan, Esc stop.
    /// Additional debug hotkeys: O = orientation diagnostic, I = toggle InvertSteer (tape), F = toggle FrontWheelDrive (tape).
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

        // detection helpers
        Transform detectedMazeWalls = null;
        int lastMazeWallsChildCount = -1;

        // debug visualization
        bool debugDrawInitDir = false;
        Vector3 debugTrajDir = Vector3.zero;
        Vector3 debugCarFwd = Vector3.forward;

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

        /// <summary>
        /// Try to auto-detect a MazeWalls GameObject and attach it to WallPointsGroups.
        /// Also watches for child-count changes to automatically rebuild wall geometry.
        /// </summary>
        void TryAutoAttachMazeWalls()
        {
            if (!AutoFindMazeWalls) return;

            var go = GameObject.Find("MazeWalls");
            if (go == null)
            {
                detectedMazeWalls = null;
                lastMazeWallsChildCount = -1;
                return;
            }

            bool contains = false;
            for (int i = 0; i < WallPointsGroups.Count; i++) if (WallPointsGroups[i] == go.transform) { contains = true; break; }
            if (!contains && WallPointsGroups.Count == 0)
            {
                WallPointsGroups.Add(go.transform);
                BuildWallsFromGroups();
            }

            if (detectedMazeWalls != go.transform)
            {
                detectedMazeWalls = go.transform;
                lastMazeWallsChildCount = detectedMazeWalls.childCount;
            }
            else
            {
                int cur = detectedMazeWalls != null ? detectedMazeWalls.childCount : -1;
                if (cur != lastMazeWallsChildCount)
                {
                    lastMazeWallsChildCount = cur;
                    BuildWallsFromGroups();
                }
            }
        }

        void Update()
        {
            TryAutoAttachMazeWalls();

            // debug hotkeys
            if (Input.GetKeyDown(KeyCode.O)) // orientation diagnostic
            {
                RunOrientationDiagnostic();
            }
            if (Input.GetKeyDown(KeyCode.I)) // toggle invert steer on tape executor
            {
                if (exec is TapeExecutor te)
                {
                    te.InvertSteer = !te.InvertSteer;
                    Debug.Log($"TapeExecutor.InvertSteer = {te.InvertSteer}");
                }
            }
            if (Input.GetKeyDown(KeyCode.F)) // toggle FWD/RWD
            {
                if (exec is TapeExecutor te)
                {
                    te.FrontWheelDrive = !te.FrontWheelDrive;
                    Debug.Log($"TapeExecutor.FrontWheelDrive = {te.FrontWheelDrive}");
                }
            }

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
