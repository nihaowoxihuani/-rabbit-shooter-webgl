using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] public Transform target;
    [SerializeField] public float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}
