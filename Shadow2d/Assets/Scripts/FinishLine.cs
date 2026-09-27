using UnityEngine;

// Trigger at the end of the course. Shows the run time while playing and
// a finish message once the player crosses it.
public class FinishLine : MonoBehaviour
{
    private float startTime;
    private float finishTime = -1f;

    private void Start() => startTime = Time.time;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (finishTime < 0f && other.CompareTag("Player"))
            finishTime = Time.time - startTime;
    }

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
        float t = finishTime >= 0f ? finishTime : Time.time - startTime;
        string time = $"{(int)t / 60}:{t % 60f:00.0}";
        GUI.Label(new Rect(0, 10, Screen.width, 40), time, style);
        if (finishTime >= 0f)
        {
            style.fontSize = 56;
            GUI.Label(new Rect(0, Screen.height / 2f - 40, Screen.width, 80), "FINISH!", style);
        }
    }
}
