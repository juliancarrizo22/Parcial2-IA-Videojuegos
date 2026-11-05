

using System.Collections.Generic;
using UnityEngine;

public class BoidAgent : MonoBehaviour
{
    [Header("Hunter")]
    [SerializeField] private float hunterPerceptionRadius = 10f;
    [SerializeField] private float evadeWeight = 2f;

    [Header("Interest")]
    [SerializeField] private float interestPerceptionRadius = 12f;
    [SerializeField] private float arriveSlowingDistance = 3f;
    [SerializeField] private float interestReachDistance = 0.8f;

    [Header("Movement")]
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float maxSteering = 3f;

    [Header("Boundary")]
    [SerializeField] private float boundaryMargin = 2f;
    [SerializeField] private float boundaryWeight = 4f;

    [Header("Perception")]
    [SerializeField] private float separationRadius = 2f;
    [SerializeField] private float alignmentRadius = 5f;
    [SerializeField] private float cohesionRadius = 5f;
    [SerializeField] private float perceptionInterval = 0.15f;

    [Header("Weights")]
    [SerializeField] private float separationWeight = 1f;
    [SerializeField] private float alignmentWeight = 1f;
    [SerializeField] private float cohesionWeight = 1f;

    private Vector3 velocity;
    private BoidHealth health;

    private Collider[] perceptionBuffer = new Collider[64];

    private List<BoidAgent> nearbyBoids = new List<BoidAgent>();
    private List<InterestObject> nearbyInterests =
        new List<InterestObject>();

    private HunterAgent nearbyHunter;

    private float perceptionTimer;
    private float perceptionRadius;

    public Vector3 Velocity => velocity;

    private void Awake()
    {
        health = GetComponent<BoidHealth>();

        perceptionRadius = Mathf.Max(
            hunterPerceptionRadius,
            interestPerceptionRadius,
            separationRadius,
            alignmentRadius,
            cohesionRadius
        );
    }

    private void Start()
    {
        InitializeMovement();
    }

    public void InitializeMovement()
    {
        Vector3 direction = Random.insideUnitSphere;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector3.forward;

        velocity = direction.normalized * maxSpeed;
        perceptionTimer = perceptionInterval;
    }

    private void Update()
    {
        if (health != null && health.IsDead)
        {
            velocity = Vector3.zero;
            return;
        }

        perceptionTimer += Time.deltaTime;

        if (perceptionTimer >= perceptionInterval)
        {
            RefreshPerception();
            perceptionTimer = 0f;
        }

        Vector3 steering;

        if (nearbyHunter != null &&
            Vector3.Distance(transform.position,
                nearbyHunter.transform.position) <= hunterPerceptionRadius)
        {
            steering = GetEvade(nearbyHunter) * evadeWeight;
        }
        else
        {
            InterestObject interest = FindInterest();

            if (interest != null)
            {
                steering = GetArrive(interest.transform);

                if (Vector3.Distance(
                    transform.position,
                    interest.transform.position) <= interestReachDistance)
                {
                    if (InterestManager.Instance != null)
                        InterestManager.Instance.RemoveInterest(interest);
                }
            }
            else
            {
                steering =
                    GetSeparation() * separationWeight +
                    GetAlignment() * alignmentWeight +
                    GetCohesion() * cohesionWeight;
            }
        }

        steering += GetBoundarySteering() * boundaryWeight;

        steering = Vector3.ClampMagnitude(steering, maxSteering);

        velocity += steering * Time.deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        transform.position += velocity * Time.deltaTime;

        transform.position = new Vector3(
            transform.position.x,
            1f,
            transform.position.z
        );

        if (velocity.sqrMagnitude > 0.001f)
            transform.forward = velocity.normalized;

        KeepInsideBounds();
    }

    private void RefreshPerception()
    {
        nearbyBoids.Clear();
        nearbyInterests.Clear();
        nearbyHunter = null;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            perceptionRadius,
            perceptionBuffer
        );

        float closestHunterDistance = Mathf.Infinity;

        for (int i = 0; i < count; i++)
        {
            Collider col = perceptionBuffer[i];

            if (col == null)
                continue;

            BoidAgent otherBoid = col.GetComponentInParent<BoidAgent>();

            if (otherBoid != null)
            {
                if (otherBoid != this && !nearbyBoids.Contains(otherBoid))
                    nearbyBoids.Add(otherBoid);

                continue;
            }

            HunterAgent hunter = col.GetComponentInParent<HunterAgent>();

            if (hunter != null)
            {
                float distance = (
                    hunter.transform.position - transform.position
                ).sqrMagnitude;

                if (distance < closestHunterDistance)
                {
                    closestHunterDistance = distance;
                    nearbyHunter = hunter;
                }

                continue;
            }

            InterestObject interest =
                col.GetComponentInParent<InterestObject>();

            if (interest != null && !nearbyInterests.Contains(interest))
                nearbyInterests.Add(interest);
        }
    }

    private BoidAgent[] GetNeighbors(float radius)
    {
        List<BoidAgent> neighbors = new List<BoidAgent>();
        float radiusSquared = radius * radius;

        foreach (BoidAgent other in nearbyBoids)
        {
            if (other == null)
                continue;

            float distanceSquared = (
                other.transform.position - transform.position
            ).sqrMagnitude;

            if (distanceSquared <= radiusSquared)
                neighbors.Add(other);
        }

        return neighbors.ToArray();
    }


    private Vector3 GetSeparation()
    {
        Vector3 force = Vector3.zero;
        float radiusSquared = separationRadius * separationRadius;

        foreach (BoidAgent other in nearbyBoids)
        {
            if (other == null)
                continue;

            Vector3 away = transform.position - other.transform.position;
            float distanceSquared = away.sqrMagnitude;

            if (distanceSquared > 0.001f &&
                distanceSquared <= radiusSquared)
            {
                // CAMBIO CLAVE: Dividir por distanceSquared (inverso al cuadrado) 
                // hace que la fuerza de repulsión explote y sea muchísimo más intensa 
                // cuanto más cerca estén el uno del otro, evitando que se amontonen.
                force += away.normalized / distanceSquared;
            }
        }

        return SteerTowards(force);
    }

    private Vector3 GetAlignment()
    {
        Vector3 averageVelocity = Vector3.zero;
        float radiusSquared = alignmentRadius * alignmentRadius;
        int neighborCount = 0;

        foreach (BoidAgent other in nearbyBoids)
        {
            if (other == null)
                continue;

            float distanceSquared =
                (other.transform.position - transform.position).sqrMagnitude;

            if (distanceSquared <= radiusSquared)
            {
                averageVelocity += other.Velocity;
                neighborCount++;
            }
        }

        if (neighborCount == 0)
            return Vector3.zero;

        averageVelocity /= neighborCount;

        return SteerTowards(averageVelocity);
    }

    private Vector3 GetCohesion()
    {
        Vector3 center = Vector3.zero;
        float radiusSquared = cohesionRadius * cohesionRadius;
        int neighborCount = 0;

        foreach (BoidAgent other in nearbyBoids)
        {
            if (other == null)
                continue;

            float distanceSquared =
                (other.transform.position - transform.position).sqrMagnitude;

            if (distanceSquared <= radiusSquared)
            {
                center += other.transform.position;
                neighborCount++;
            }
        }

        if (neighborCount == 0)
            return Vector3.zero;

        center /= neighborCount;

        return SteerTowards(center - transform.position);
    }


    private Vector3 SteerTowards(Vector3 desired)
    {
        if (desired.sqrMagnitude < 0.001f)
            return Vector3.zero;

        Vector3 desiredVelocity = desired.normalized * maxSpeed;

        return Vector3.ClampMagnitude(
            desiredVelocity - velocity,
            maxSteering
        );
    }

    private Vector3 GetEvade(HunterAgent hunter)
    {
        Vector3 futurePosition = hunter.transform.position
            + hunter.Velocity * 0.5f;

        Vector3 direction = transform.position - futurePosition;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = Random.insideUnitSphere;

        direction.y = 0f;

        return SteerTowards(direction);
    }

    private InterestObject FindInterest()
    {
        InterestObject closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (InterestObject interest in nearbyInterests)
        {
            if (interest == null)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                interest.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = interest;
            }
        }

        return closest;
    }

    private Vector3 GetArrive(Transform target)
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance < 0.01f)
            return -velocity;

        float desiredSpeed = maxSpeed;

        if (distance < arriveSlowingDistance)
            desiredSpeed *= distance / arriveSlowingDistance;

        Vector3 desiredVelocity = direction.normalized * desiredSpeed;

        return Vector3.ClampMagnitude(
            desiredVelocity - velocity,
            maxSteering
        );
    }

    private void KeepInsideBounds()
    {
        if (BoidBounds.Instance == null)
            return;

        transform.position = BoidBounds.Instance.KeepInsideBounds(
            transform.position,
            ref velocity
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, alignmentRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, cohesionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            Mathf.Max(
                hunterPerceptionRadius,
                interestPerceptionRadius
            )
        );
    }

    private Vector3 GetBoundarySteering()
    {
        if (BoidBounds.Instance == null)
            return Vector3.zero;

        Vector3 center = BoidBounds.Instance.transform.position;
        Vector3 localPosition = transform.position - center;

        Vector2 halfSize = BoidBounds.Instance.BoundsSize / 2f;

        Vector3 force = Vector3.zero;

        if (localPosition.x > halfSize.x - boundaryMargin)
            force.x -= 1f;
        else if (localPosition.x < -halfSize.x + boundaryMargin)
            force.x += 1f;

        if (localPosition.z > halfSize.y - boundaryMargin)
            force.z -= 1f;
        else if (localPosition.z < -halfSize.y + boundaryMargin)
            force.z += 1f;

        return SteerTowards(force);
    }
}


