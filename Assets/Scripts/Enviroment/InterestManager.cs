
using System.Collections.Generic;
using UnityEngine;

public class InterestManager : MonoBehaviour
{
    public static InterestManager Instance { get; private set; }

    [Header("Spawning")]
    [SerializeField] private InterestObject interestPrefab;
    [SerializeField] private int maxActiveInterests = 5;

    private readonly List<InterestObject> activeInterests =
        new List<InterestObject>();

    public int ActiveCount => activeInterests.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SpawnInterest()
    {
        activeInterests.RemoveAll(item => item == null);

        if (interestPrefab == null ||
            activeInterests.Count >= maxActiveInterests)
            return;

        if (BoidBounds.Instance == null)
            return;

        Vector3 position = BoidBounds.Instance.GetRandomPosition();

        // RESTRICCIÓN DE MARGEN: Forzamos un pequeño colchón para que 
        // no aparezcan pegados contra los bordes extremos del mapa.
        Vector3 center = BoidBounds.Instance.transform.position;
        Vector2 boundsSize = BoidBounds.Instance.BoundsSize;
        float margin = 3f; // Margen de seguridad respecto a las paredes

        position.x = Mathf.Clamp(position.x, center.x - (boundsSize.x / 2f) + margin, center.x + (boundsSize.x / 2f) - margin);
        position.z = Mathf.Clamp(position.z, center.y - (boundsSize.y / 2f) + margin, center.y + (boundsSize.y / 2f) - margin);

        position.y = 0.5f;

        InterestObject interest = Instantiate(
            interestPrefab,
            position,
            Quaternion.identity
        );

        activeInterests.Add(interest);
    }

    public void RemoveInterest(InterestObject interest)
    {
        if (interest == null)
            return;

        activeInterests.Remove(interest);
        Destroy(interest.gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}

