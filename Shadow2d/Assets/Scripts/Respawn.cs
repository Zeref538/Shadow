using UnityEngine;
using UnityEngine.SceneManagement;

// Sends the player back to the starting line when they fall off the map
// or leave the background art. Attaches itself to the "Player" in every
// scene, so no scene setup is needed.
public class Respawn : MonoBehaviour
{
    public float killY = -10f;      // fallback when there's no Background
    public float margin = 3f;       // how far past the background edge counts as out

    private Bounds bgBounds;
    private bool hasBg;
    private bool respawning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        AttachToPlayer();
        SceneManager.sceneLoaded += (s, m) => AttachToPlayer();
    }

    private static void AttachToPlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<Respawn>() == null)
            player.AddComponent<Respawn>();
    }

    private void Start()
    {
        var bg = GameObject.Find("Background");
        if (bg != null)
        {
            foreach (var r in bg.GetComponentsInChildren<Renderer>())
            {
                if (!hasBg) { bgBounds = r.bounds; hasBg = true; }
                else bgBounds.Encapsulate(r.bounds);
            }
        }
    }

    private void Update()
    {
        Vector3 p = transform.position;
        bool outOfMap;
        if (hasBg)
            outOfMap = p.y < bgBounds.min.y - margin
                    || p.x < bgBounds.min.x - margin
                    || p.x > bgBounds.max.x + margin;
        else
            outOfMap = p.y < killY;

        if (outOfMap) DoRespawn();
    }

    // Reloading the scene puts the player back on the starting line AND
    // resets every prop - broken falling platforms, shoved crates - so the
    // course is always finishable after a fall.
    public void DoRespawn()
    {
        if (respawning) return;
        respawning = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
