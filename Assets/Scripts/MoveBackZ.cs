using UnityEngine;

public sealed class MoveBackZ : MonoBehaviour
{
    [SerializeField] private float speed = 1f;

    private Vector3 startPosition;

    private void Awake()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        transform.position = startPosition + Vector3.back * (speed * Time.time);
    }
}
