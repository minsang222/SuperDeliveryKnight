using UnityEngine;

public class PlatformManager : MonoBehaviour
{
    [SerializeField] private GameObject[] buildings;
    [SerializeField] private GameObject[] obstacles;
    [SerializeField] private float spawnOutsideDistance = 5f;
    [SerializeField] private float ranOffsetYRange = 0.5f;

    [SerializeField] private float ranOffsetXRangeMax = 5f;
    [SerializeField] private float ranOffsetXRangeMin = 2f;


    private PlatBuilding _lastBuilding;
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Start()
    {
        SpawnPlatform(new Vector3(GetSpawnX(), 0f, 0f));
    }

    private void Update()
    {
        if (_lastBuilding.EndPoint.position.x <= GetSpawnX())
        {
            Vector3 nextPosition = _lastBuilding.EndPoint.position + new Vector3(
                Random.Range(ranOffsetXRangeMin, ranOffsetXRangeMax),
                Random.Range(-ranOffsetYRange, ranOffsetYRange));

            SpawnPlatform(nextPosition);
        }
    }

    private void SpawnPlatform(Vector3 startPosition)
    {
        GameObject prefab = buildings[Random.Range(0, buildings.Length)];
        PlatBuilding building = Instantiate(prefab, transform).GetComponent<PlatBuilding>();

        Vector3 startPointOffset = building.StartPoint.position - building.transform.position;
        building.transform.position = startPosition - startPointOffset;

        SpawnObstacles(building);

        _lastBuilding = building;
    }

    private void SpawnObstacles(PlatBuilding building)
    {
        if (obstacles == null || obstacles.Length == 0 || building.ObstaclePoints == null)
        {
            return;
        }

        foreach (Transform obstaclePoint in building.ObstaclePoints)
        {
            if (obstaclePoint == null)
            {
                continue;
            }

            int obstacleIndex = Random.Range(0, obstacles.Length + 1);

            if (obstacleIndex == obstacles.Length || obstacles[obstacleIndex] == null)
            {
                continue;
            }

            GameObject obstacle = Instantiate(obstacles[obstacleIndex], obstaclePoint.position, obstaclePoint.rotation);
        }
    }


    private float GetSpawnX()
    {
        return _mainCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, -_mainCamera.transform.position.z)).x + spawnOutsideDistance;
    }

}
