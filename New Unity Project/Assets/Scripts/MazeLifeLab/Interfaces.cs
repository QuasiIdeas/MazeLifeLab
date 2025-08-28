using System.Collections.Generic;

namespace MazeLifeLab
{
    /// <summary>Planner interface for kinodynamic RRT planner.</summary>
    public interface IRRTPlanner
    {
        /// <summary>Reset planner with start and goal states.</summary>
        void Reset(CarState start, CarState goal);

        /// <summary>Perform planning iterations. Returns true if solution found.</summary>
        bool Step(int iterations = 1);

        /// <summary>Whether a solution was found.</summary>
        bool HasSolution { get; }

        /// <summary>Extract the time-stamped state trajectory when a solution exists.</summary>
        Trajectory ExtractTrajectory();

        /// <summary>Extract control tape of (u, dt, N) segments corresponding to the trajectory.</summary>
        List<(CarControl u, float dt, int N)> ExtractTape();

        /// <summary>Number of nodes in the search tree.</summary>
        int NodeCount { get; }
    }

    /// <summary>Executor interface for running trajectories on a car model / WheelColliders.</summary>
    public interface IExecutor
    {
        void Load(Trajectory traj, List<(CarControl u, float dt, int N)> tape = null);
        void Start();
        void TickFixed(float fixedDt, UnityEngine.Transform carRoot, UnityEngine.WheelCollider fl, UnityEngine.WheelCollider fr, UnityEngine.WheelCollider rl, UnityEngine.WheelCollider rr);
        void Stop();
        bool Completed { get; }
        float LateralError { get; }
        float HeadingError { get; }
    }

    // placeholders for future phases
    public interface IObstacleSource { }
    public interface IObstaclePredictor { }
    public interface ITimeCollisionChecker { }
}
