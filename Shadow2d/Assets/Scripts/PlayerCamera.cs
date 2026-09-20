using UnityEngine;

// Smooth camera follow with a small look-ahead in the direction you are
// running, so you see where you are going instead of where you have been.
//
// Uses SmoothDamp rather than MoveTowards: MoveTowards travels at a fixed
// speed and stops abruptly, which is what makes a follow camera feel stiff.
// SmoothDamp eases in and out, like a car with suspension instead of one
// bolted straight to the axle.
public class PlayerCamera : MonoBehaviour
{
    public Transform target;

    [Tooltip("Lower = snappier. 0.15-0.25 feels good for a platformer.")]
    public float smoothTime = 0.18f;

    [Tooltip("How far ahead the camera leads you when running.")]
    public float lookAhead = 2.5f;

    public Vector2 offset = new Vector2(0f, 1.2f);

    [Tooltip("Camera never drops below this, so it stops showing empty space under the level.")]
    public float minY = 1f;

    Rigidbody2D targetBody;
    Vector3 velocity;   // SmoothDamp needs to keep its own velocity between frames
    float ahead;

    void Start()
    {
        if (target != null) targetBody = target.GetComponent<Rigidbody2D>();
        if (target != null) transform.position = Goal();
    }

    // LateUpdate, not Update: the player moves in FixedUpdate, and following
    // after everything else has moved stops the camera juddering a frame behind.
    void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.SmoothDamp(transform.position, Goal(),
                                                ref velocity, smoothTime);
    }

    Vector3 Goal()
    {
        float vx = targetBody != null ? targetBody.linearVelocity.x : 0f;
        // Only lead once actually moving, otherwise the camera drifts while idle.
        float wanted = Mathf.Abs(vx) > 0.5f ? Mathf.Sign(vx) * lookAhead : 0f;
        ahead = Mathf.Lerp(ahead, wanted, Time.deltaTime * 3f);

        return new Vector3(
            target.position.x + offset.x + ahead,
            Mathf.Max(target.position.y + offset.y, minY),
            transform.position.z);
    }
}
