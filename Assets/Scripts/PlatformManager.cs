using UnityEngine;
using UnityEngine.Serialization;

public class PlatformManager : MonoBehaviour
{
    public static PlatformManager Instance { get; private set; }
    public int DefaultPositionRandomSeed { get; private set; }

    [SerializeField, FormerlySerializedAs("_buildings")] private GameObject[] buildings;
    [SerializeField, FormerlySerializedAs("_obstacles")] private GameObject[] obstacles;
    [SerializeField] private Player player;
    [Header("Building Chain")]
    [Tooltip("씬에 미리 배치한 첫 건물의 EndPoint Transform")]
    [SerializeField] private Transform firstBuildingEndPoint;
    [SerializeField, FormerlySerializedAs("_spawnOutsideDistance"), FormerlySerializedAs("spawnOutsideDistance"), Min(0f)]
    private float nextBuildingSpawnDistance = 5f;
    [Header("Finish Building")]
    [SerializeField] private GameObject endBuilding;
    [SerializeField, Min(0f)] private float endBuildingSpawnDistance = 100f;
    [SerializeField, FormerlySerializedAs("_randomOffsetXRangeMax"), FormerlySerializedAs("ranOffsetXRangeMax")]
    private float randomOffsetXRangeMax = 5f;
    [SerializeField, FormerlySerializedAs("_randomOffsetXRangeMin"), FormerlySerializedAs("ranOffsetXRangeMin")]
    private float randomOffsetXRangeMin = 2f;
    
    private System.Random _positionRandom;
    private PlatBuilding _firstBuilding;
    private PlatBuilding _lastGeneratedBuilding;
    private Transform _lastEndPoint;
    private float _firstEndPointX;
    private bool _hasSpawnedEndBuilding;

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
        _positionRandom = new System.Random(DefaultPositionRandomSeed);
    }

    private void Start()
    {
        if (firstBuildingEndPoint == null)
        {
            Debug.LogError("PlatformManager에 첫 건물의 EndPoint를 지정해야 합니다.", this);
            return;
        }

        _firstBuilding = firstBuildingEndPoint.GetComponentInParent<PlatBuilding>();
        _lastEndPoint = firstBuildingEndPoint;
        _firstEndPointX = firstBuildingEndPoint.position.x;
    }

    private void Update()
    {
        // 마지막 EndPoint까지의 남은 거리가 설정값 이하가 되면 다음 건물을 잇는다.
        if (!_hasSpawnedEndBuilding && ShouldSpawnNextBuilding())
        {
            Vector3 nextPosition = _lastEndPoint.position + new Vector3(
                NextPositionRange(randomOffsetXRangeMin, randomOffsetXRangeMax),
                0f);

            bool shouldSpawnEndBuilding = _lastEndPoint.position.x >= _firstEndPointX + endBuildingSpawnDistance;
            if (shouldSpawnEndBuilding && endBuilding != null)
            {
                SpawnPlatform(nextPosition, endBuilding);
                _hasSpawnedEndBuilding = true;
            }
            else
            {
                SpawnPlatform(nextPosition);
            }
        }
    }

    private bool ShouldSpawnNextBuilding()
    {
        if (_lastEndPoint == null || player == null)
        {
            return false;
        }

        float distanceToLastEndPoint = _lastEndPoint.position.x - player.transform.position.x;
        return distanceToLastEndPoint <= nextBuildingSpawnDistance;
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
        SpawnPlatform(startPosition, buildings[Random.Range(0, buildings.Length)]);
    }

    private void SpawnPlatform(Vector3 startPosition, GameObject prefab)
    {
        PlatBuilding building = Instantiate(prefab, transform).GetComponent<PlatBuilding>();

        // 프리팹마다 피벗 위치가 달라도 StartPoint끼리 이어지도록 보정한다.
        Vector3 startPointOffset = building.StartPoint.position - building.transform.position;
        building.transform.position = startPosition - startPointOffset;

        if (_lastEndPoint != null && player != null)
        {
            Vector2 margin = player.CanReachChunk(_lastEndPoint, building.StartPoint);
            if (margin.x < 0f)
            {
                building.transform.position += new Vector3(margin.x, 0f, 0f);
            }

            // 낮은 수평 간격에서는 점프 궤적의 안전 여유가 부족할 수 있다. 이전에는
            // 다음 청크를 아래로 내려 이를 보정해 청크가 누적 하강했다. 높이는 유지하고
            // 필요한 만큼만 오른쪽으로 옮겨 도달 가능성을 확보한다.
            const float horizontalAdjustmentStep = 0.1f;
            const int maxHorizontalAdjustments = 100;
            for (int i = 0; i < maxHorizontalAdjustments; i++)
            {
                margin = player.CanReachChunk(_lastEndPoint, building.StartPoint);
                if (margin.y >= 0f || margin.x <= 0f)
                {
                    break;
                }

                building.transform.position += Vector3.right * horizontalAdjustmentStep;
            }
        }

        SpawnObstacles(building);

        _lastGeneratedBuilding = building;
        _lastEndPoint = building.EndPoint;
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

            // 건물 프리팹 자체가 확대되어 있어 부모를 지정한 채 생성하면 장애물도
            // 건물의 스케일을 상속받는다. 먼저 월드 좌표/스케일로 생성한 뒤 부모를
            // 연결해 장애물 프리팹의 의도된 크기를 유지한다.
            GameObject obstacle = Instantiate(
                obstacles[obstacleIndex], obstaclePoint.position, obstaclePoint.rotation);
            obstacle.transform.SetParent(building.transform, true);
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

        nextBuilding ??= _lastGeneratedBuilding != null
            ? _lastGeneratedBuilding
            : _firstBuilding;
        if (nextBuilding == null || nextBuilding.StartPoint == null)
        {
            position = default;
            return false;
        }

        position = nextBuilding.StartPoint.position + Vector3.up * dropHeight;
        return true;
    }
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

}
