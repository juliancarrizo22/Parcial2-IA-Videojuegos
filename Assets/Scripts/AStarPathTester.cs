
using System.Collections.Generic;
using UnityEngine;

public class AStarPathTester : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private AStarPathfinder pathfinder;
    [SerializeField] private GraphNode startNode;
    [SerializeField] private GraphNode goalNode;

    private List<GraphNode> currentPath = new List<GraphNode>();

    [ContextMenu("Test A* Path")]
    private void TestPath()
    {
        if (pathfinder == null || startNode == null || goalNode == null)
        {
            Debug.LogError("AStarPathTester: faltan referencias.");
            return;
        }

        currentPath = pathfinder.FindPath(startNode, goalNode);

        if (currentPath.Count == 0)
        {
            Debug.LogWarning("A*: no se encontro una ruta.");
            return;
        }

        Debug.Log("A*: ruta encontrada con " +
            currentPath.Count + " nodos.");

        foreach (GraphNode node in currentPath)
        {
            Debug.Log("Ruta: " + node.name);
        }
    }

    private void OnDrawGizmos()
    {
        if (currentPath == null || currentPath.Count < 2)
            return;

        Gizmos.color = Color.magenta;

        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Gizmos.DrawLine(
                currentPath[i].transform.position,
                currentPath[i + 1].transform.position
            );
        }
    }
}

