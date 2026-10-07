using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector2 offset = new Vector2(2f, 1.5f);
    public float smoothTime = 0.2f;
    public float minX = -4f;
    public float maxX = 90f;
    public float minY = 0f;
    public float maxY = 10f;

    Vector3 velocity;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 desired = new Vector3(
            Mathf.Clamp(target.position.x + offset.x, minX, maxX),
            Mathf.Clamp(target.position.y + offset.y, minY, maxY),
            transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
    }
}
