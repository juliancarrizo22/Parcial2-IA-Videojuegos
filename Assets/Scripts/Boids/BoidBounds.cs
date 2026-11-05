
using UnityEngine;

public class BoidBounds : MonoBehaviour
{
    public static BoidBounds Instance { get; private set; }

    [Header("Bounds del Coto")]
    [SerializeField] private Vector2 boundsSize = new Vector2(22f, 26f);
    public Vector2 BoundsSize => boundsSize;
    private void Awake()
    {
        Instance = this;
    }

    public Vector3 GetRandomPosition()
    {
        float x = Random.Range(-boundsSize.x / 2f, boundsSize.x / 2f);
        float z = Random.Range(-boundsSize.y / 2f, boundsSize.y / 2f);

        return transform.position + new Vector3(x, 1f, z);
    }

    public Vector3 KeepInsideBounds(Vector3 position, ref Vector3 velocity)
    {
        Vector3 localPosition = position - transform.position;

        float halfWidth = boundsSize.x / 2f;
        float halfHeight = boundsSize.y / 2f;

        if (localPosition.x > halfWidth)
        {
            localPosition.x = halfWidth;
            velocity.x = -Mathf.Abs(velocity.x);
        }
        else if (localPosition.x < -halfWidth)
        {
            localPosition.x = -halfWidth;
            velocity.x = Mathf.Abs(velocity.x);
        }

        if (localPosition.z > halfHeight)
        {
            localPosition.z = halfHeight;
            velocity.z = -Mathf.Abs(velocity.z);
        }
        else if (localPosition.z < -halfHeight)
        {
            localPosition.z = -halfHeight;
            velocity.z = Mathf.Abs(velocity.z);
        }

        return transform.position + localPosition;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        Vector3 size = new Vector3(boundsSize.x, 0.1f, boundsSize.y);

        Gizmos.DrawWireCube(transform.position + Vector3.up, size);
    }
}

