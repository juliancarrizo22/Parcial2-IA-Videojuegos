
using System.Collections.Generic;
using UnityEngine;

public class GraphNode : MonoBehaviour
{
    [Header("Conexiones con otros nodos")]
    public List<GraphNode> neighbors = new List<GraphNode>();

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.25f);

        Gizmos.color = Color.yellow;

        foreach (GraphNode neighbor in neighbors)
        {
            if (neighbor != null)
            {
                Gizmos.DrawLine(
                    transform.position,
                    neighbor.transform.position
                );
            }
        }
    }
}
