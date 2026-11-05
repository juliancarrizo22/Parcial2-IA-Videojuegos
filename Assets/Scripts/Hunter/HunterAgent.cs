using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum HunterState
{
    Patrol,
    Attack,
    Gather,
    Flee,
    Exit,
    Dead
}

public class HunterAgent : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waypointDistance = 0.8f;

    [Header("Navigation")]
    [SerializeField] private AStarPathfinder pathfinder;
    [SerializeField] private GraphNode[] searchNodes;

    private int searchNodeIndex;
    [SerializeField] private float nodeReachDistance = 0.5f;

    private List<GraphNode> currentPath = new List<GraphNode>();
    private int currentPathIndex;

    [Header("Perception")]
    [SerializeField] private float perceptionRadius = 12f;
    [SerializeField] private float perceptionInterval = 0.15f;
    [SerializeField] private float lostTargetTolerance = 1.5f;

    [Header("Combat")]
    [SerializeField] private float tba = 3f;
    [SerializeField] private float rangeAttackRadius = 4f;
    [SerializeField] private float meleeAttackRadius = 2f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Gather and Respawn")]
    [SerializeField] private float gatherDistance = 1.5f;
    [SerializeField] private float gatherTime = 2f;
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private float respawnWidth = 20f;
    [SerializeField] private float respawnHeight = 20f;

    [Header("Interest Spawning")]
    [SerializeField] private float interestSpawnInterval = 5f;

    [Header("Parcial 2 - Configuración de Reaparición")]
    [SerializeField] private float respawnTime = 5f;
    [SerializeField] private Transform initialZoneTransform;

    [Header("Parcial 2 - Flee & Exit Config")]
    [SerializeField] private float wolfPerceptionRadius = 10f;
    [SerializeField] private LayerMask wolfMask;
    [SerializeField] private Transform[] safeZones;
    [SerializeField] private Transform exitZone;
    [SerializeField] private int requiredGathers = 3;
    private int currentGathers = 0;
    [SerializeField] private float safeZoneWaitTime = 4f;
    private float safeZoneTimer = 0f;

    private HunterState currentState;
    private float lastAttackTime = -Mathf.Infinity;
    private float gatherTimer;
    private float interestSpawnTimer;
    private float perceptionTimer;
    private float gatherAttemptTimer;
    private float currentRespawnTimer = 0f;

    private BoidAgent target;
    private BoidAgent gatherTarget;
    private HunterVisualFeedback visualFeedback;

    private Collider[] perceptionBuffer = new Collider[64];
    private List<BoidAgent> detectedBoids = new List<BoidAgent>();

    public HunterState CurrentState => currentState;
    public BoidAgent Target => target;
    public float PerceptionRadius => perceptionRadius;
    public Vector3 Velocity { get; private set; }

    private void Start()
    {
        if (pathfinder == null)
            pathfinder = FindFirstObjectByType<AStarPathfinder>();

        RefreshPerception();
        ChangeState(HunterState.Patrol);
    }

    private void Update()
    {
        perceptionTimer += Time.deltaTime;

        if (perceptionTimer >= perceptionInterval)
        {
            RefreshPerception();
            perceptionTimer = 0f;
        }

        // Comprobar si hay lobos cerca para huir (Prioridad máxima)
        if (currentState != HunterState.Dead && currentState != HunterState.Flee && CheckForWolves())
        {
            ChangeState(HunterState.Flee);
        }

        switch (currentState)
        {
            case HunterState.Patrol:
                UpdatePatrol();
                break;
            case HunterState.Attack:
                UpdateAttack();
                break;
            case HunterState.Gather:
                UpdateGather();
                break;
            case HunterState.Flee:
                UpdateFlee();
                break;
            case HunterState.Exit:
                UpdateExit();
                break;
            case HunterState.Dead:
                UpdateDead();
                break;
        }
    }

    // MÉTODO CLAVE: Calcula distancia plana (ignorando Y) para evitar bloqueos en nodos
    private float GetFlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private bool CheckForWolves()
    {
        Collider[] wolfHits = Physics.OverlapSphere(transform.position, wolfPerceptionRadius, wolfMask);
        return wolfHits.Length > 0;
    }

    private void RefreshPerception()
    {
        detectedBoids.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, perceptionRadius, perceptionBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider col = perceptionBuffer[i];
            if (col == null) continue;

            BoidAgent boid = col.GetComponentInParent<BoidAgent>();
            if (boid == null || detectedBoids.Contains(boid)) continue;

            detectedBoids.Add(boid);
        }
    }

    private void ChangeState(HunterState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        currentPath.Clear();
        currentPathIndex = 0;

        if (newState == HunterState.Gather)
        {
            gatherTimer = 0f;
        }

        if (visualFeedback != null)
        {
            if (newState == HunterState.Gather) visualFeedback.ShowGather();
            else if (newState == HunterState.Patrol) visualFeedback.ShowNormal();
        }
    }

    private void UpdatePatrol()
    {
        BoidAgent deadBoid = FindBoid(true);
        if (deadBoid != null)
        {
            gatherTarget = deadBoid;
            currentPath.Clear();
            ChangeState(HunterState.Gather);
            return;
        }

        if (Time.time >= lastAttackTime + tba)
        {
            BoidAgent aliveBoid = FindBoid(false);
            if (aliveBoid != null)
            {
                target = aliveBoid;
                currentPath.Clear();
                ChangeState(HunterState.Attack);
                return;
            }
        }

        interestSpawnTimer += Time.deltaTime;
        if (interestSpawnTimer >= interestSpawnInterval)
        {
            interestSpawnTimer = 0f;
            if (InterestManager.Instance != null)
                InterestManager.Instance.SpawnInterest();
        }

        if (searchNodes == null || searchNodes.Length == 0) return;

        GraphNode destination = searchNodes[searchNodeIndex];
        if (destination == null) return;

        float distance = GetFlatDistance(transform.position, destination.transform.position);

        if (distance <= waypointDistance || (currentPath.Count > 0 && currentPathIndex >= currentPath.Count))
        {
            searchNodeIndex = (searchNodeIndex + 1) % searchNodes.Length;
            currentPath.Clear();
            currentPathIndex = 0;
            return;
        }

        MoveAlongPath(destination.transform.position, true);
    }

    private void UpdateAttack()
    {
        if (target == null)
        {
            currentPath.Clear();
            ChangeState(HunterState.Patrol);
            return;
        }

        BoidHealth health = target.GetComponent<BoidHealth>();
        if (health == null || health.IsDead)
        {
            target = null;
            currentPath.Clear();
            ChangeState(HunterState.Patrol);
            return;
        }

        float distance = GetFlatDistance(transform.position, target.transform.position);

        if (distance > perceptionRadius + lostTargetTolerance)
        {
            target = null;
            currentPath.Clear();
            ChangeState(HunterState.Patrol);
            return;
        }

        if (distance > rangeAttackRadius || !HasLineOfSight(target.transform.position))
        {
            MoveAlongPath(target.transform.position, true);
            return;
        }

        health.TakeDamage(attackDamage);
        lastAttackTime = Time.time;
        target = null;
        currentPath.Clear();
        ChangeState(HunterState.Patrol);

        if (visualFeedback != null) visualFeedback.ShowAttack();
    }

    private void UpdateGather()
    {
        if (gatherTarget == null)
        {
            gatherAttemptTimer = 0f;
            ChangeState(HunterState.Patrol);
            return;
        }

        BoidHealth health = gatherTarget.GetComponent<BoidHealth>();
        if (health == null || !health.IsDead)
        {
            gatherTarget = null;
            gatherAttemptTimer = 0f;
            ChangeState(HunterState.Patrol);
            return;
        }

        float distance = GetFlatDistance(transform.position, gatherTarget.transform.position);

        if (distance > gatherDistance)
        {
            MoveAlongPath(gatherTarget.transform.position, true);
            gatherAttemptTimer += Time.deltaTime;

            if (gatherAttemptTimer > 4f)
            {
                gatherTarget = null;
                gatherAttemptTimer = 0f;
                currentPath.Clear();
                ChangeState(HunterState.Patrol);
            }
            return;
        }

        gatherAttemptTimer = 0f;
        StopMovement();
        gatherTimer += Time.deltaTime;

        if (gatherTimer >= gatherTime)
        {
            BoidAgent collectedBoid = gatherTarget;
            gatherTarget = null;

            collectedBoid.gameObject.SetActive(false);
            StartCoroutine(RespawnBoid(collectedBoid));

            currentPath.Clear();
            currentPathIndex = 0;
            currentGathers++;

            if (currentGathers >= requiredGathers && exitZone != null)
            {
                ChangeState(HunterState.Exit);
            }
            else
            {
                ChangeState(HunterState.Patrol);
            }
        }
    }

    private void UpdateFlee()
    {
        Transform nearestSafeZone = GetNearestSafeZone();
        if (nearestSafeZone == null)
        {
            ChangeState(HunterState.Patrol);
            return;
        }

        float distToSafe = GetFlatDistance(transform.position, nearestSafeZone.position);

        if (distToSafe > 1.5f)
        {
            MoveAlongPath(nearestSafeZone.position, false);
        }
        else
        {
            StopMovement();
            safeZoneTimer += Time.deltaTime;

            if (safeZoneTimer >= safeZoneWaitTime || !CheckForWolves())
            {
                safeZoneTimer = 0f;
                ChangeState(HunterState.Patrol);
            }
        }
    }

    private void UpdateExit()
    {
        if (exitZone == null)
        {
            ChangeState(HunterState.Patrol);
            return;
        }

        float distToExit = GetFlatDistance(transform.position, exitZone.position);

        if (distToExit > 1.5f)
        {
            MoveAlongPath(exitZone.position, true);
        }
        else
        {
            // 1. Reiniciar contadores y limpiar referencias de objetivos
            currentGathers = 0;
            target = null;
            gatherTarget = null;

            // 2. Desactivar temporalmente el CharacterController para evitar bloqueos al teletransportar
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            // 3. Teletransportar de regreso a la Zona Inicial (igual que en la reaparición por muerte)
            if (initialZoneTransform != null)
            {
                transform.position = initialZoneTransform.position;
            }
            else
            {
                transform.position = Vector3.zero;
            }

            if (controller != null) controller.enabled = true;

            // 4. Reiniciar el índice de búsqueda al nodo más cercano y limpiar la ruta actual
            searchNodeIndex = GetNearestSearchNodeIndex();
            currentPath.Clear();

            // 5. Volver al estado de Patrulla para iniciar un nuevo ciclo hacia el coto
            ChangeState(HunterState.Patrol);

            Debug.Log("[HUNTER] ¡Ciclo de salida completado! Reapareció en la Zona Inicial y reinició la búsqueda hacia el coto.");
        }
    }

    private void UpdateDead()
    {
        currentRespawnTimer += Time.deltaTime;

        if (currentRespawnTimer >= respawnTime)
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            if (initialZoneTransform != null)
            {
                transform.position = initialZoneTransform.position;
            }
            else
            {
                transform.position = Vector3.zero;
            }

            if (controller != null) controller.enabled = true;

            ToggleVisuals(true);
            currentRespawnTimer = 0f;

            searchNodeIndex = GetNearestSearchNodeIndex();
            currentPath.Clear();
            ChangeState(HunterState.Patrol);
        }
    }

    public void KillHunter()
    {
        if (currentState == HunterState.Dead) return;

        currentState = HunterState.Dead;
        currentRespawnTimer = 0f;
        ToggleVisuals(false);

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
    }

    private Transform GetNearestSafeZone()
    {
        if (safeZones == null || safeZones.Length == 0) return null;
        Transform closest = null;
        float minDst = Mathf.Infinity;

        foreach (var sz in safeZones)
        {
            if (sz == null) continue;
            float dst = GetFlatDistance(transform.position, sz.position);
            if (dst < minDst)
            {
                minDst = dst;
                closest = sz;
            }
        }
        return closest;
    }

    private int GetNearestSearchNodeIndex()
    {
        if (searchNodes == null || searchNodes.Length == 0) return 0;
        int closestIndex = 0;
        float minDst = Mathf.Infinity;

        for (int i = 0; i < searchNodes.Length; i++)
        {
            if (searchNodes[i] == null) continue;
            float dst = GetFlatDistance(transform.position, searchNodes[i].transform.position);
            if (dst < minDst)
            {
                minDst = dst;
                closestIndex = i;
            }
        }
        return closestIndex;
    }

    private BoidAgent FindBoid(bool findDead)
    {
        BoidAgent closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (BoidAgent boid in detectedBoids)
        {
            if (boid == null) continue;

            BoidHealth health = boid.GetComponent<BoidHealth>();
            if (health == null || health.IsDead != findDead) continue;

            if (!findDead && !HasLineOfSight(boid.transform.position)) continue;

            float distance = (transform.position - boid.transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = boid;
            }
        }

        return closest;
    }

    private void MoveAlongPath(Vector3 destination, bool allowDirectFinal = false)
    {
        if (allowDirectFinal && HasLineOfSight(destination))
        {
            currentPath.Clear();
            MoveTowards(destination);
            return;
        }

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

        bool needsNewPath = currentPath.Count == 0 || currentPath[currentPath.Count - 1] != goalNode;

        if (needsNewPath)
        {
            currentPath = pathfinder.FindPath(startNode, goalNode);
            currentPathIndex = 0;

            if (currentPath == null || currentPath.Count == 0)
            {
                currentPath.Clear();
                searchNodeIndex = (searchNodeIndex + 1) % searchNodes.Length;
                StopMovement();
                return;
            }
        }

        while (currentPathIndex < currentPath.Count &&
            GetFlatDistance(transform.position, currentPath[currentPathIndex].transform.position) <= nodeReachDistance)
        {
            currentPathIndex++;
        }

        if (currentPathIndex < currentPath.Count)
        {
            MoveTowards(currentPath[currentPathIndex].transform.position);
        }
        else if (allowDirectFinal && HasLineOfSight(destination))
        {
            MoveTowards(destination);
        }
        else
        {
            currentPath.Clear();
            searchNodeIndex = (searchNodeIndex + 1) % searchNodes.Length;
            StopMovement();
        }
    }

    private void MoveTowards(Vector3 destination)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            StopMovement();
            return;
        }

        direction.Normalize();
        Velocity = direction * speed;

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.Move(Velocity * Time.deltaTime);
        }
        else
        {
            transform.position += Velocity * Time.deltaTime;
        }

        transform.forward = direction;
    }

    private void StopMovement()
    {
        Velocity = Vector3.zero;
    }

    private IEnumerator RespawnBoid(BoidAgent boid)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (boid == null) yield break;

        boid.transform.position = new Vector3(
            Random.Range(-respawnWidth, respawnWidth),
            1f,
            Random.Range(-respawnHeight, respawnHeight)
        );

        BoidHealth health = boid.GetComponent<BoidHealth>();
        if (health != null) health.Revive();

        boid.InitializeMovement();
        boid.gameObject.SetActive(true);
    }

    private void ToggleVisuals(bool isVisible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.enabled = isVisible;
        }
    }

    private bool HasLineOfSight(Vector3 destination)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = destination - origin;

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
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangeAttackRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, meleeAttackRadius);
    }

    private void Awake()
    {
        visualFeedback = GetComponent<HunterVisualFeedback>();
    }
}