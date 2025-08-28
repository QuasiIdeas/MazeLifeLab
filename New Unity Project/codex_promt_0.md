You are an expert Unity/C# engineer. 
Target: Unity 2021+ C#, physics with WheelCollider. 
Namespace: MazeLifeLab. 
Coding style: clean, commented, production-ready. 
Use structs for small data containers, classes for systems. 
No external packages.
All angles in radians internally; convert to degrees only for WheelCollider.steerAngle.
All code must compile.


Create file: Assets/Scripts/MazeLifeLab/Core/Types.cs

Goal:
- Define core data types for Phase 2 (kinodynamic RRT).
- Provide a robust Trajectory container with helpers.

Requirements:
- namespace MazeLifeLab;
- public struct CarState { float X, Y, Theta, V; }
- public struct CarControl { float Accel, Steer; } // a, δ (radians)
- public static class Mathx:
  - WrapAngle(rad): (-π, π]
  - Clamp(value, min, max)
- public sealed class Trajectory:
  - public List<float> T;  // timestamps, strictly increasing
  - public List<CarState> S; // same length as T
  - Methods:
    * Clear(), Count, Duration
    * Append(float t, CarState s)
    * SampleByTime(float t) -> CarState (linear interp; wrap Theta)
    * NearestByArc(CarState pose, int startIdx=0) -> int
    * ToWorldPolyline() -> Vector3[]  // Y->Z for Unity world
- XML-doc comments and summary on all public members.

Acceptance:
- Compiles.
- SampleByTime clamps to [0,Duration].
- ToWorldPolyline maps (X,Y)->(x,0,z).


Create file: Assets/Scripts/MazeLifeLab/Core/Dynamics.cs

Goal:
- Implement kinematic bicycle model and RK4 integrator.

Requirements:
- public sealed class Dynamics:
  - public float L = 2.6f;
  - public float VMin = -5f, VMax = 12f;
  - public float SteerMax = 0.6f;   // rad
  - public float AccelMax = 3f;     // m/s^2
  - public float LatAccelMax = 6f;  // m/s^2 limit for curvature speed
  - public CarState F(CarState s, CarControl u)
  - public CarState RK4(CarState s, CarControl u, float dt)
  - public CarState ClampState(CarState s)
  - public float CurvatureFromSteer(float v, float steer) => tan(steer)/L
  - public float SpeedLimitFromCurvature(float kappa) => kappa==0 ? VMax : Mathf.Sqrt(Mathf.Max(0.01f, LatAccelMax/Mathf.Abs(kappa)));
- Ensure theta wrapping and v clamping after RK4.
- Guard against NaN on tan(steer) near ±π/2 by clamping steer to SteerMax.

Acceptance:
- Compiles.
- RK4 reduces to Euler if dt is very small.
- For u=(0,0) and v>0, X,Y advance along theta.


Create file: Assets/Scripts/MazeLifeLab/Collision/Collision2D.cs

Goal:
- Fast collision checks of car footprint vs static walls.

Requirements:
- public sealed class Collision2D:
  - public float CarLength = 4.3f, CarWidth = 1.9f, Inflation = 0.15f;
  - public List<Vector2[]> Walls; // each polyline in XY
  - public void SetWalls(List<Vector2[]> walls)
  - public bool Collides(CarState s)
  - public bool SegmentRolloutCollides(List<CarState> rollout, int stride=1)
- Car footprint = rectangle centered near rear-axle to mid-body:
  - place rear axle at (X,Y), theta.
  - rectangle center = (X + 0.5f*CarLength*cosθ, Y + 0.5f*CarLength*sinθ)
  - half-extents = (CarLength*0.5f + Inflation, CarWidth*0.5f + Inflation)
- Approx walls as thickened segments: point-to-segment distance < half-width threshold -> collision.
- Provide static helper DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b).

Acceptance:
- Compiles.
- Given empty walls, returns false.
- With a wall segment crossing the car footprint, returns true.


Create file: Assets/Scripts/MazeLifeLab/Interfaces.cs

Goal:
- Define minimal interfaces to keep MVP modular.

Requirements:
- public interface IRRTPlanner {
    void Reset(CarState start, CarState goal);
    bool Step(int iterations=1);  // returns true if solution found
    bool HasSolution { get; }
    Trajectory ExtractTrajectory();          // time-stamped states
    List<(CarControl u, float dt, int N)> ExtractTape(); // control segments
    int NodeCount { get; }
  }

- public interface IExecutor {
    void Load(Trajectory traj, List<(CarControl u, float dt, int N)> tape=null);
    void Start();
    void TickFixed(float fixedDt, Transform carRoot, WheelCollider fl, WheelCollider fr, WheelCollider rl, WheelCollider rr);
    void Stop();
    bool Completed { get; }
    float LateralError { get; }
    float HeadingError { get; }
  }

- public interface IObstacleSource {}              // placeholder for Phase 3
- public interface IObstaclePredictor {}           // placeholder for Phase 3
- public interface ITimeCollisionChecker {}        // placeholder for Phase 3

Acceptance:
- Compiles.


Create file: Assets/Scripts/MazeLifeLab/Planning/RRTPlanner.cs

Goal:
- Kinodynamic RRT that expands by integrating Dynamics.RK4 with piecewise-constant controls.

Requirements:
- public sealed class RRTPlanner : IRRTPlanner
- ctor(Dynamics dyn, Collision2D col, System.Random rng)
- Config (public):
  - float Dt = 0.05f;
  - float EdgeTime = 0.75f;  // seconds
  - int   GoalBiasPercent = 10;
  - int   MaxNodes = 8000;
  - float Wx=1f, Wtheta=0.5f, Wv=0.2f; // nearest metric weights
  - CarControl[] ControlSet; // if null, build default grid of (a,δ)
- Internal node:
  struct Node {
    public CarState s;
    public int parent;
    public CarControl u;
    public float segDt; public int segN;
    public List<CarState> rollout;
  }
- Methods:
  - Reset(start, goal): clears nodes; add root.
  - Step(iterations): repeatedly:
    * sample xRand (goal-biased)
    * find nearest node by weighted metric
    * sample control u from ControlSet
    * integrate segN = round(EdgeTime/Dt) RK4 steps, collect rollout
    * check curvature-speed feasibility (use dyn.SpeedLimitFromCurvature)
    * abort if collision at any step; else add node
    * if goal reached (pos + yaw tolerances), mark solution and store goal index
  - ExtractTrajectory(): backtrack nodes to root, reverse; produce time-stamped states with uniform dt (Dt).
  - ExtractTape(): from each edge produce (u, Dt, segN) and concatenate.
- Public props: HasSolution, NodeCount

Acceptance:
- Compiles.
- With trivial walls and straight start→goal, nodes grow and solution emerges.


Create file: Assets/Scripts/MazeLifeLab/Control/TapeExecutor.cs

Goal:
- Play back recorded (a, δ, dt, N) control segments frame-by-frame.

Requirements:
- public sealed class TapeExecutor : IExecutor
- Fields:
  - List<(CarControl u, float dt, int N)> tape;
  - int segIdx, tickInSeg;
  - float steerDeg; // cached
  - float motorTorque, brakeTorque;
- Config (public):
  - float MaxMotorTorque = 1200f;
  - float MaxBrakeTorque = 2500f;
  - float AccelToTorque = 400f; // simple mapping a->torque
- Load(traj, tape?) stores both; Start() resets indices.
- In TickFixed:
  - If finished => Completed=true; zero outputs; return.
  - Get current segment, apply u=(a,δ):
    * steerAngle = δ * Mathf.Rad2Deg on FL/FR
    * torque = Mathf.Clamp(u.Accel * AccelToTorque, -MaxBrakeTorque, MaxMotorTorque)
    * if torque >= 0: motorTorque on FL/FR, brake=0; else motor=0, brake = |torque| on all wheels
  - Advance tickInSeg; if == N -> segIdx++.
- Errors: none; this executor does not correct drift (manager will stop if errors too big).
- Expose LateralError/HeadingError as 0 for now.

Acceptance:
- Compiles.
- Runs without null-ref when tape is provided.

Create file: Assets/Scripts/MazeLifeLab/Control/TrackerExecutor.cs

Goal:
- Follow time-stamped trajectory using Pure Pursuit (steer) and PI speed control.

Requirements:
- public sealed class TrackerExecutor : IExecutor
- Public config:
  - float LookaheadMin = 1.5f;
  - float LookaheadK = 0.5f;        // Ld = max(LookaheadMin, LookaheadK * v)
  - float KpSpeed = 300f, KiSpeed = 40f;
  - float MaxMotorTorque = 1200f, MaxBrakeTorque = 2500f;
- State:
  - Trajectory traj; int cursor; float integ;
  - Completed flag; cached latest errors.
- TickFixed:
  - Read car world pose from carRoot (position.xz and yaw from transform.eulerAngles.y).
  - Compute current ref time t += fixedDt; find target point s_ref at t and a lookahead point s_look along arc/time.
  - Compute lateral error via Pure Pursuit geometry; command steer δ.
  - Speed control: error_v = v_ref - v_meas; torque = Kp*err + Ki*∫err dt; map to motor/brake torque.
  - Apply to WheelColliders (steer FL/FR; torque on FL/FR; brake on all when negative).
  - Completed when near final point with small speed.
- Expose LateralError and HeadingError.

Acceptance:
- Compiles.
- On a straight trajectory, car aligns and follows.


Create file: Assets/Scripts/MazeLifeLab/Managers/RRTManager.cs

Goal:
- Central controller: Manual → Planning → Executing(Tape|Track).
- Draw RRT tree/path; handle hotkeys.

Requirements:
- public sealed class RRTManager : MonoBehaviour
- Inspector refs:
  - Transform CarRoot; WheelCollider FL, FR, RL, RR;
  - List<Transform> WallPointsGroups; // each group -> polyline
  - bool UseTracker = true;  // else Tape
- Internal:
  - Dynamics dyn; Collision2D col; IRRTPlanner planner;
  - IExecutor exec;
  - enum State { Manual, Planning, ExecutingTape, ExecutingTrack }
  - State state;
  - CarState start, goal;
  - Coroutine planLoop;
  - List<(Vector3 a, Vector3 b, Color c)> debugLines; // tree edges
  - Trajectory lastTraj; List<(CarControl u,float dt,int N)> lastTape;
- OnStart:
  - Build walls from groups into Collision2D.
  - Instantiate dyn, col, planner.
- Update:
  - Hotkeys: M (Manual/Auto), 1 (Tape), 2 (Tracker), R (replan), Esc (Stop).
  - If state==Planning: show live tree.
- FixedUpdate:
  - If state Executing*: exec.TickFixed(Time.fixedDeltaTime, CarRoot, FL,FR,RL,RR)
  - If exec.Completed -> state=Manual
- Public methods:
  - BeginPlanning(): set start from CarRoot pose; planner.Reset(start, goal); start coroutine PlannerLoop()
  - IEnumerator PlannerLoop(): while !HasSolution && nodes<MaxNodes -> planner.Step(32); collect edges for debug; on success ExtractTrajectory/Tape and switch to Executing state.
- OnDrawGizmos: draw walls, tree (grey), rejected (red), final path (green), current ref polyline (blue).

Acceptance:
- Compiles.
- Toggles modes and runs basic end-to-end with empty/simple walls.

Create file: Assets/Scripts/MazeLifeLab/UI/DebugHUD.cs

Goal:
- Simple overlay with planner/executor stats.

Requirements:
- public sealed class DebugHUD : MonoBehaviour
- public RRTManager Manager;
- OnGUI(): draw box with:
  - State, NodeCount, HasSolution
  - Last search time, path length, duration
  - Executor: Completed?, LateralError, HeadingError
- Lightweight IMGUI only.

Acceptance:
- Compiles.
