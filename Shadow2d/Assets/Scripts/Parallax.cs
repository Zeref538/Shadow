using UnityEngine;

// Scrolls a background layer slower than the camera so it reads as distant.
// factor 0 = pinned to the camera (infinitely far), 1 = moves with the world.
public class Parallax : MonoBehaviour
{
    public float factor = 0.5f;
    public float verticalFactor = 0.3f;

    Transform cam;
    Vector3 startSelf;
    Vector3 startCam;

    void Start()
    {
        cam = Camera.main.transform;
        startSelf = transform.position;
        startCam = cam.position;
    }

    // LateUpdate, not Update: the camera moves in FixedUpdate, and running
    // after it stops the background lagging a frame behind and shimmering.
    void LateUpdate()
    {
        Vector3 moved = cam.position - startCam;
        transform.position = new Vector3(
            startSelf.x + moved.x * factor,
            startSelf.y + moved.y * verticalFactor,
            startSelf.z);
    }
}
