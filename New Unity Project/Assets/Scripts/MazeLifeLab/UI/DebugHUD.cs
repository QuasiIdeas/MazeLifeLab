using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Simple IMGUI HUD showing planner and executor stats.
    /// </summary>
    public sealed class DebugHUD : MonoBehaviour
    {
        public RRTManager Manager;
        Rect box = new Rect(10, 10, 320, 140);

        void OnGUI()
        {
            if (Manager == null) return;
            GUILayout.BeginArea(box, GUI.skin.box);
            GUILayout.Label($"State: {Manager.GetType().Name}");
            // try to reflect some properties
            var plannerField = typeof(RRTManager).GetField("planner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (plannerField != null)
            {
                var planner = plannerField.GetValue(Manager) as IRRTPlanner;
                if (planner != null)
                {
                    GUILayout.Label($"Nodes: {planner.NodeCount}");
                    GUILayout.Label($"HasSolution: {planner.HasSolution}");
                }
            }

            // executor
            var execField = typeof(RRTManager).GetField("exec", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (execField != null)
            {
                var exec = execField.GetValue(Manager) as IExecutor;
                if (exec != null)
                {
                    GUILayout.Label($"Exec Completed: {exec.Completed}");
                    GUILayout.Label($"LatErr: {exec.LateralError:F2}");
                    GUILayout.Label($"HeadErr: {exec.HeadingError:F2}");
                }
            }

            GUILayout.EndArea();
        }
    }
}
