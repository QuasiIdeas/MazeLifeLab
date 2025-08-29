#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MazeLifeLab
{
    [CustomEditor(typeof(RRTManager))]
    public class RRTManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(6);
            if (GUILayout.Button("Make Scene Screenshot"))
            {
                var mgr = target as RRTManager;
                if (mgr != null) mgr.MakeSceneScreenshot();
            }
        }
    }
}
#endif
