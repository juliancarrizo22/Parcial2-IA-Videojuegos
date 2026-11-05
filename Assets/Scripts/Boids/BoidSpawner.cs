
using UnityEngine;

public class BoidSpawner : MonoBehaviour
{
    [SerializeField] private BoidAgent boidPrefab;
    [SerializeField] private int boidCount = 6;
    [SerializeField] private float spawnWidth = 15f;
    [SerializeField] private float spawnHeight = 15f;

    private void Start()
    {
        if (boidPrefab == null)
        {
            Debug.LogError("BoidSpawner: falta asignar el prefab.");
            return;
        }

        for (int i = 0; i < boidCount; i++)
        {
            Vector3 position = new Vector3(
                Random.Range(-spawnWidth, spawnWidth),
                1f,
                Random.Range(-spawnHeight, spawnHeight)
            );

            Instantiate(boidPrefab, position, Quaternion.identity);
        }
    }
}

