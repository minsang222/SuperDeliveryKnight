using UnityEngine;
using UnityEngine.Serialization;

public class PlatformManager : MonoBehaviour
{
    public static PlatformManager Instance { get; private set; }
    public int DefaultPositionRandomSeed { get; private set; }

    [SerializeField, FormerlySerializedAs("_buildings")] private GameObject[] buildings;
    [SerializeField, FormerlySerializedAs("_obstacles")] private GameObject[] obstacles;
    [SerializeField, FormerlySerializedAs("_spawnOutsideDistance")] private float spawnOutsideDistance = 5f;
    [SerializeField] private Player player;
    [SerializeField, FormerlySerializedAs("_randomOffsetYRange"), FormerlySerializedAs("ranOffsetYRange")]
    private float randomOffsetYRange = 0.5f;

    [SerializeField, FormerlySerializedAs("_randomOffsetXRangeMax"), FormerlySerializedAs("ranOffsetXRangeMax")]
    private float randomOffsetXRangeMax = 5f;
    [SerializeField, FormerlySerializedAs("_randomOffsetXRangeMin"), FormerlySerializedAs("ranOffsetXRangeMin")]
    private float randomOffsetXRangeMin = 2f;
    
    private PlatBuilding _lastBuilding;
    private Camera _mainCamera;
    private System.Random _positionRandom;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("PlatformMananger 중복");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DefaultPositionRandomSeed = 0;
        _mainCamera = Camera.main;
        _positionRandom = new System.Random(DefaultPositionRandomSeed);
    }

    private void Start()
    {
        SpawnPlatform(new Vector3(GetSpawnX(), 0f, 0f));
    }

    private void Update()
    {
        // 마지막 건물 끝이 화면 앞쪽 여유 구간에 들어오면 다음 건물을 미리 준비한다.
        if (_lastBuilding.EndPoint.position.x <= GetSpawnX())
        {
            Vector3 nextPosition = _lastBuilding.EndPoint.position + new Vector3(
                NextPositionRange(randomOffsetXRangeMin, randomOffsetXRangeMax),
                NextPositionRange(-randomOffsetYRange, randomOffsetYRange));

            SpawnPlatform(nextPosition);
        }
    }

    public void SetPositionRandomSeed(int seed)
    {
        _positionRandom = new System.Random(seed);
    }

    private float NextPositionRange(float minInclusive, float maxExclusive)
    {
        return minInclusive + (float)_positionRandom.NextDouble() * (maxExclusive - minInclusive);
    }

    private void SpawnPlatform(Vector3 startPosition)
    {
        GameObject prefab = buildings[Random.Range(0, buildings.Length)];
        PlatBuilding building = Instantiate(prefab, transform).GetComponent<PlatBuilding>();

        // 프리팹마다 피벗 위치가 달라도 StartPoint끼리 이어지도록 보정한다.
        Vector3 startPointOffset = building.StartPoint.position - building.transform.position;
        building.transform.position = startPosition - startPointOffset;

        if (_lastBuilding != null && player != null)
        {
            Vector2 margin = player.CanReachChunk(_lastBuilding.EndPoint, building.StartPoint);
            if (margin.x < 0f)
            {
                building.transform.position += new Vector3(margin.x, 0f, 0f);
            }

            margin = player.CanReachChunk(_lastBuilding.EndPoint, building.StartPoint);
            if (margin.y < 0f)
            {
                building.transform.position += new Vector3(0f, margin.y, 0f);
            }
        }

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

            // 배열 밖의 한 칸을 '배치하지 않음'으로 써서 빈 지점도 같은 확률로 섞는다.
            int obstacleIndex = Random.Range(0, obstacles.Length + 1);

            if (obstacleIndex == obstacles.Length || obstacles[obstacleIndex] == null)
            {
                continue;
            }

            Instantiate(obstacles[obstacleIndex], obstaclePoint.position, obstaclePoint.rotation, building.transform);
        }
    }

    public bool TryGetRespawnPoint(float playerX, float dropHeight, out Vector3 position)
    {
        PlatBuilding nextBuilding = null;

        foreach (PlatBuilding building in GetComponentsInChildren<PlatBuilding>())
        {
            if (building.StartPoint == null || building.StartPoint.position.x <= playerX)
            {
                continue;
            }

            if (nextBuilding == null || building.StartPoint.position.x < nextBuilding.StartPoint.position.x)
            {
                nextBuilding = building;
            }
        }

        nextBuilding ??= _lastBuilding;
        if (nextBuilding == null || nextBuilding.StartPoint == null)
        {
            position = default;
            return false;
        }

        position = nextBuilding.StartPoint.position + Vector3.up * dropHeight;
        return true;
    }


    private float GetSpawnX()
    {
        return _mainCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, -_mainCamera.transform.position.z)).x + spawnOutsideDistance;
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

}
