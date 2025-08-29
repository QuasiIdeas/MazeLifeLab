#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MazeLifeLab
{
    /// <summary>
    /// Editor helper for RRTPlanner: adds a "Make Screenshot" button to capture the Scene view to a PNG file.
    /// This captures the Scene window (not the Game window) and writes the image to the Screenshots folder.
    /// </summary>
    [CustomEditor(typeof(RRTPlanner))]
    public sealed class RRTPlannerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(6);
            if (GUILayout.Button("Make Screenshot (Scene View)"))
            {
                MakeSceneViewScreenshot();
            }
        }

        static void MakeSceneViewScreenshot()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                Debug.LogWarning("No active SceneView. Open the Scene window and try again.");
                return;
            }

            var cam = sv.camera;
            if (cam == null)
            {
                Debug.LogWarning("SceneView camera is not available.");
                return;
            }

            int w = cam.pixelWidth;
            int h = cam.pixelHeight;
            if (w <= 0 || h <= 0)
            {
                // fallback to a reasonable size
                w = 1600; h = 900;
            }

            var rt = new RenderTexture(w, h, 24);
            var prev = RenderTexture.active;
            var prevCamTarget = cam.targetTexture;

            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();

                string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string filename = $"SceneShot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string path = Path.Combine(dir, filename);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"Scene screenshot saved: {path}");

                // ensure file shows up in Unity editor Assets if needed
                AssetDatabase.Refresh();

                UnityEngine.Object.DestroyImmediate(tex);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to capture SceneView: " + ex.Message);
            }
            finally
            {
                cam.targetTexture = prevCamTarget;
                RenderTexture.active = prev;
                if (rt != null) rt.Release();
            }
        }
    }
}
#endif
