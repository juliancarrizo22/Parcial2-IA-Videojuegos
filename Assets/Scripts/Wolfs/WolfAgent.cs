using System.Collections.Generic;
using UnityEngine;

public enum WolfState
{
    Patrol,
    Chase,
    SearchLastKnown
}

public class WolfAgent : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 3f;
    [SerializeField] private GraphNode[] patrolNodes;
    private int patrolNodeIndex = 0;
    [SerializeField] private float nodeReachDistance = 0.5f;
    [SerializeField] private float captureRadius = 1.5f;

    [Header("Perception (FOV / Line of Sight)")]
    [SerializeField] private float viewRadius = 10f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private LayerMask hunterMask;

    [Header("Navigation")]
    [SerializeField] private AStarPathfinder pathfinder;

    [Header("Search Config")]
    [SerializeField] private float maxSearchTime = 5f; // Tiempo que se quedan vigilando la zona
    private float searchTimer = 0f;

    private List<GraphNode> currentPath = new List<GraphNode>();
    private int currentPathIndex = 0;

    private WolfState currentState;
    private HunterAgent targetHunter;
    private Vector3 lastKnownHunterPosition;
    private CharacterController characterController;
    private bool hasAlertedPack = false;
    private Vector3 lastDestinationChecked = Vector3.negativeInfinity;

    private void Start()
    {
        if (pathfinder == null)
            pathfinder = FindFirstObjectByType<AStarPathfinder>();

        characterController = GetComponent<CharacterController>();

        if (WolfManager.Instance != null)
            WolfManager.Instance.RegisterWolf(this);

        ChangeState(WolfState.Patrol);
    }

    private void OnDestroy()
    {
        if (WolfManager.Instance != null)
            WolfManager.Instance.UnregisterWolf(this);
    }

    private void Update()
    {
        HunterAgent spottedHunter = CheckForHunter();

        if (spottedHunter != null)
        {
            targetHunter = spottedHunter;
            lastKnownHunterPosition = targetHunter.transform.position;

            if (!hasAlertedPack)
            {
                hasAlertedPack = true;
                if (WolfManager.Instance != null)
                {
                    WolfManager.Instance.BroadcastHowlAlert(this, lastKnownHunterPosition);
                }
            }

            if (currentState != WolfState.Chase)
            {
                ChangeState(WolfState.Chase);
            }
        }
        else
        {
            hasAlertedPack = false;

            if (currentState == WolfState.Chase)
            {
                ChangeState(WolfState.SearchLastKnown);
            }
        }

        switch (currentState)
        {
            case WolfState.Patrol:
                UpdatePatrol();
                break;
            case WolfState.Chase:
                UpdateChase();
                break;
            case WolfState.SearchLastKnown:
                UpdateSearchLastKnown();
                break;
        }
    }

    // Método que se ejecuta cuando OTRO lobo aúlla
    public void ReceiveHowlAlert(Vector3 alertPosition)
    {
        if (currentState == WolfState.Patrol || currentState == WolfState.SearchLastKnown)
        {
            lastKnownHunterPosition = alertPosition;
            lastDestinationChecked = Vector3.negativeInfinity; // Forzar recálculo inmediato de ruta
            Debug.Log($"[ALERTA RECIBIDA] El lobo {name} recibió la alerta. Desplazándose a {alertPosition} mediante A*.");
            ChangeState(WolfState.SearchLastKnown);
        }
    }

    private float GetFlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void ChangeState(WolfState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        currentPath.Clear();
        currentPathIndex = 0;
        lastDestinationChecked = Vector3.negativeInfinity;
        searchTimer = 0f;
    }

    private void UpdatePatrol()
    {
        if (patrolNodes == null || patrolNodes.Length == 0) return;

        GraphNode destinationNode = patrolNodes[patrolNodeIndex];
        if (destinationNode == null) return;

        float distance = GetFlatDistance(transform.position, destinationNode.transform.position);

        if (distance <= nodeReachDistance || (currentPath.Count > 0 && currentPathIndex >= currentPath.Count))
        {
            patrolNodeIndex = (patrolNodeIndex + 1) % patrolNodes.Length;
            currentPath.Clear();
            currentPathIndex = 0;
            return;
        }

        MoveAlongPath(destinationNode.transform.position);
    }

    private void UpdateChase()
    {
        if (targetHunter == null)
        {
            ChangeState(WolfState.SearchLastKnown);
            return;
        }

        lastKnownHunterPosition = targetHunter.transform.position;
        float distance = GetFlatDistance(transform.position, targetHunter.transform.position);

        if (distance <= captureRadius)
        {
            targetHunter.KillHunter();
            StopMovement();
            ChangeState(WolfState.Patrol);
            return;
        }

        // Si lo tiene a la vista, se mueve directo hacia él sin recalcular A* en cada frame
        MoveToward(targetHunter.transform.position);
    }

    private void UpdateSearchLastKnown()
    {
        MoveAlongPath(lastKnownHunterPosition);

        // Si el lobo llegó al final del camino permitido (la frontera del Coto o Zona Segura)
        if (currentPath != null && currentPath.Count > 0 && currentPathIndex >= currentPath.Count)
        {
            StopMovement();
            searchTimer += Time.deltaTime;

            // Se queda vigilando y buscando alrededor del perímetro durante 'maxSearchTime' 
            // antes de retomar su patrulla normal por waypoints.
            if (searchTimer >= maxSearchTime)
            {
                searchTimer = 0f;
                ChangeState(WolfState.Patrol);
            }
        }
        else
        {
            searchTimer = 0f; // Reinicia si sigue en movimiento hacia la zona
        }
    }

    private List<GraphNode> GetFilteredPath(GraphNode start, GraphNode goal)
    {
        if (pathfinder == null || NavigationGraph.Instance == null) return null;

        List<GraphNode> rawPath = pathfinder.FindPath(start, goal);
        if (rawPath == null) return null;

        List<GraphNode> filteredPath = new List<GraphNode>();

        foreach (var node in rawPath)
        {
            if (node == null) continue;

            // Restricción: prohibido pisar Coto o Zonas Seguras
            if (node.name.Contains("Coto") || node.name.Contains("Safe"))
            {
                break;
            }

            filteredPath.Add(node);
        }

        return filteredPath;
    }

    private void MoveAlongPath(Vector3 destination)
    {
        if (pathfinder == null || NavigationGraph.Instance == null)
        {
            StopMovement();
            return;
        }

        GraphNode goalNode = NavigationGraph.Instance.GetNearestNode(destination);
        GraphNode startNode = NavigationGraph.Instance.GetNearestNode(transform.position);

        if (startNode == null || goalNode == null)
        {
            StopMovement();
            return;
        }

        bool needsNewPath = currentPath.Count == 0 || Vector3.Distance(lastDestinationChecked, destination) > 1.5f;

        if (needsNewPath)
        {
            currentPath = GetFilteredPath(startNode, goalNode);
            currentPathIndex = 0;
            lastDestinationChecked = destination;

            if (currentPath == null || currentPath.Count == 0)
            {
                currentPath.Clear();
                StopMovement();
                ChangeState(WolfState.Patrol);
                return;
            }

            // CORRECCIÓN DE RETROCESO: Si el primer nodo está detrás del lobo, saltamos al índice 1
            if (currentPath.Count > 1)
            {
                Vector3 dirToFirstNode = (currentPath[0].transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, dirToFirstNode) < 0f || GetFlatDistance(transform.position, currentPath[0].transform.position) <= nodeReachDistance)
                {
                    currentPathIndex = 1;
                }
            }
        }

        Vector3 flatAgentPos = new Vector3(transform.position.x, 0f, transform.position.z);

        while (currentPathIndex < currentPath.Count &&
            GetFlatDistance(flatAgentPos, new Vector3(currentPath[currentPathIndex].transform.position.x, 0f, currentPath[currentPathIndex].transform.position.z)) <= nodeReachDistance)
        {
            currentPathIndex++;
        }

        if (currentPathIndex < currentPath.Count)
        {
            MoveToward(currentPath[currentPathIndex].transform.position);
        }
        else
        {
            StopMovement();
        }
    }

    private void MoveToward(Vector3 destination)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            StopMovement();
            return;
        }

        direction.Normalize();
        Vector3 velocity = direction * speed;

        if (characterController != null)
        {
            characterController.Move(velocity * Time.deltaTime);
        }
        else
        {
            transform.position += velocity * Time.deltaTime;
        }

        transform.forward = direction;
    }

    private void StopMovement() { }

    private HunterAgent CheckForHunter()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, hunterMask);

        foreach (var hit in hits)
        {
            HunterAgent hunter = hit.GetComponentInParent<HunterAgent>();
            if (hunter != null)
            {
                if (NavigationGraph.Instance != null)
                {
                    GraphNode hunterNode = NavigationGraph.Instance.GetNearestNode(hunter.transform.position);
                    if (hunterNode != null && hunterNode.name.Contains("Safe"))
                    {
                        continue;
                    }
                }

                Vector3 dirToHunter = (hunter.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dirToHunter);

                if (angle <= viewAngle / 2f)
                {
                    if (HasLineOfSight(hunter.transform.position))
                    {
                        return hunter;
                    }
                }
            }
        }
        return null;
    }

    private bool HasLineOfSight(Vector3 targetPos)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = targetPos - origin;

        return !Physics.Raycast(
            origin,
            direction.normalized,
            direction.magnitude,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, captureRadius);
    }
}