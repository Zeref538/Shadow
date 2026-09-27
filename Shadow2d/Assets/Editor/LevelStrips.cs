using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders Level.unity as four wide strips so the whole course can be checked
// by eye without playing it. Output: Logs/level_1.png .. level_4.png
public static class LevelStrips
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);
        var cam = Camera.main;
        const int w = 2400, h = 520;
        const float span = 110f;
        cam.orthographicSize = span / 2f * h / w;
        for (int i = 0; i < 4; i++)
        {
            cam.transform.position = new Vector3(span * i + span / 2f, 1.5f, -10f);
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render(); cam.Render();
            RenderTexture.active = rt;
            var shot = new Texture2D(w, h, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            File.WriteAllBytes($"Logs/level_{i + 1}.png", shot.EncodeToPNG());
            cam.targetTexture = null; RenderTexture.active = null;
        }
        Debug.Log("STRIPS done");
    }
}
