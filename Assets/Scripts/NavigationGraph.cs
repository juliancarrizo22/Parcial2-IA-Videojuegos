
using System.Collections.Generic;
using UnityEngine;

public class NavigationGraph : MonoBehaviour
{
    public static NavigationGraph Instance { get; private set; }

    [SerializeField] private List<GraphNode> nodes = new List<GraphNode>();

    public IReadOnlyList<GraphNode> Nodes => nodes;

    private void Awake()
    {
        Instance = this;

        if (nodes.Count == 0)
            nodes.AddRange(GetComponentsInChildren<GraphNode>());

        Debug.Log("NavigationGraph: " + nodes.Count + " nodos registrados.");
    }

    public GraphNode GetNearestNode(Vector3 position)
    {
        GraphNode nearest = null;
        float shortestDistance = Mathf.Infinity;

        foreach (GraphNode node in nodes)
        {
            if (node == null) continue;

            float distance = Vector3.Distance(
                position,
                node.transform.position
            );

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearest = node;
            }
        }

        return nearest;
    }
    public GraphNode GetNearestNode(Vector3 position, GraphNode excludedNode)
    {
        GraphNode nearest = null;
        float shortestDistance = Mathf.Infinity;

        foreach (GraphNode node in nodes)
        {
            if (node == null || node == excludedNode)
                continue;

            float distance = Vector3.Distance(
                position,
                node.transform.position
            );

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearest = node;
            }
        }

        return nearest;
    }
}