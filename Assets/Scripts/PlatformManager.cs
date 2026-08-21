using UnityEngine;
using UnityEngine.Serialization;

public class PlatformManager : MonoBehaviour
{
    [SerializeField] private GameObject[] buildings;
    [SerializeField] private GameObject[] obstacles;
    [SerializeField] private float spawnOutsideDistance = 5f;
    [SerializeField, FormerlySerializedAs("ranOffsetYRange")] private float randomOffsetYRange = 0.5f;

    [SerializeField, FormerlySerializedAs("ranOffsetXRangeMax")] private float randomOffsetXRangeMax = 5f;
    [SerializeField, FormerlySerializedAs("ranOffsetXRangeMin")] private float randomOffsetXRangeMin = 2f;
    
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
        // 마지막 건물 끝이 화면 앞쪽 여유 구간에 들어오면 다음 건물을 미리 준비한다.
        if (_lastBuilding.EndPoint.position.x <= GetSpawnX())
        {
            Vector3 nextPosition = _lastBuilding.EndPoint.position + new Vector3(
                Random.Range(randomOffsetXRangeMin, randomOffsetXRangeMax),
                Random.Range(-randomOffsetYRange, randomOffsetYRange));

            SpawnPlatform(nextPosition);
        }
    }

    private void SpawnPlatform(Vector3 startPosition)
    {
        GameObject prefab = buildings[Random.Range(0, buildings.Length)];
        PlatBuilding building = Instantiate(prefab, transform).GetComponent<PlatBuilding>();

        // 프리팹마다 피벗 위치가 달라도 StartPoint끼리 이어지도록 보정한다.
        Vector3 startPointOffset = building.StartPoint.position - building.transform.position;
        building.transform.position = startPosition - startPointOffset;

        var margin = GetComponent<Player>().CanReachChunk(building.StartPoint, building.transform);

        // cascaded pattern
        if (margin.x < 0)
        {
            building.transform.position += new Vector3(margin.x, 0f, 0f);
        }
        if (margin.y < 0)
        {
            building.transform.position += new Vector3(0f, margin.y, 0f);
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

            Instantiate(obstacles[obstacleIndex], obstaclePoint.position, obstaclePoint.rotation);
        }
    }


    private float GetSpawnX()
    {
        return _mainCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, -_mainCamera.transform.position.z)).x + spawnOutsideDistance;
    }

}
