using UnityEngine;
using UnityEngine.SceneManagement;

// Sends the player back to the respawn point when they fall off the map
// or leave the background art. Attaches itself to the "Player" in every
// scene, so no scene setup is needed; the respawn point is where the
// player starts unless one is assigned in the inspector.
public class Respawn : MonoBehaviour
{
    public Transform respawnPoint;
    public float killY = -10f;      // fallback when there's no Background
    public float margin = 1f;       // how far past the background edge counts as out

    private Vector3 spawnPos;
    private Rigidbody2D rb;
    private Bounds bgBounds;
    private bool hasBg;

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
        rb = GetComponent<Rigidbody2D>();
        spawnPos = respawnPoint != null ? respawnPoint.position : transform.position;

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

    public void DoRespawn()
    {
        transform.position = respawnPoint != null ? respawnPoint.position : spawnPos;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}
