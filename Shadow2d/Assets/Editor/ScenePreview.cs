using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

// Opens the saved scene and renders the Main Camera straight to a PNG, so we
// can see what the scene ON DISK actually looks like without opening the
// Editor. A RenderTexture is an off-screen canvas the GPU can draw into.
public static class ScenePreview
{
    [MenuItem("Shadow/Preview Scene To PNG")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        var cam = Camera.main;
        var player = GameObject.Find("Player");
        if (player != null)
            cam.transform.position = new Vector3(player.transform.position.x,
                                                 player.transform.position.y + 1.2f, -10f);

        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        cam.Render();   // second pass: first one warms the shaders
        RenderTexture.active = rt;
        var shot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        shot.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;

        File.WriteAllBytes("Logs/preview.png", shot.EncodeToPNG());
        Debug.Log("PROBE wrote Logs/preview.png");
    }
}
