
using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinder : MonoBehaviour
{
    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float raycastHeight = 0.5f;

    public List<GraphNode> FindPath(GraphNode start, GraphNode goal)
    {
        if (start == null || goal == null)
            return new List<GraphNode>();

        List<GraphNode> openSet = new List<GraphNode>();
        HashSet<GraphNode> closedSet = new HashSet<GraphNode>();

        Dictionary<GraphNode, GraphNode> cameFrom =
            new Dictionary<GraphNode, GraphNode>();

        Dictionary<GraphNode, float> gScore =
            new Dictionary<GraphNode, float>();

        Dictionary<GraphNode, float> fScore =
            new Dictionary<GraphNode, float>();

        openSet.Add(start);
        gScore[start] = 0f;
        fScore[start] = Heuristic(start, goal);

        while (openSet.Count > 0)
        {
            GraphNode current = GetLowestScore(openSet, fScore);

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (GraphNode neighbor in current.neighbors)
            {
                if (neighbor == null || closedSet.Contains(neighbor))
                    continue;

                if (!HasClearPath(current, neighbor))
                    continue;

                float tentativeGScore = gScore[current] +
                    Vector3.Distance(
                        current.transform.position,
                        neighbor.transform.position
                    );

                if (!gScore.ContainsKey(neighbor) ||
                    tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore +
                        Heuristic(neighbor, goal);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return new List<GraphNode>();
    }

    private float Heuristic(GraphNode a, GraphNode b)
    {
        return Vector3.Distance(
            a.transform.position,
            b.transform.position
        );
    }

    private GraphNode GetLowestScore(
        List<GraphNode> openSet,
        Dictionary<GraphNode, float> scores)
    {
        GraphNode best = openSet[0];

        foreach (GraphNode node in openSet)
        {
            if (scores.ContainsKey(node) &&
                (!scores.ContainsKey(best) || scores[node] < scores[best]))
            {
                best = node;
            }
        }

        return best;
    }

    private List<GraphNode> ReconstructPath(
        Dictionary<GraphNode, GraphNode> cameFrom,
        GraphNode current)
    {
        List<GraphNode> path = new List<GraphNode>();
        path.Add(current);

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }

        return path;
    }

    private bool HasClearPath(GraphNode from, GraphNode to)
    {
        Vector3 start = from.transform.position + Vector3.up * raycastHeight;
        Vector3 end = to.transform.position + Vector3.up * raycastHeight;

        Vector3 direction = end - start;

        return !Physics.Raycast(
            start,
            direction.normalized,
            direction.magnitude,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
    }
}

