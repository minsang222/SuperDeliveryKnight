using UnityEngine;
using UnityEngine.Serialization;

public class PlatformManager : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("buildings")] private GameObject[] _buildings;
    [SerializeField, FormerlySerializedAs("obstacles")] private GameObject[] _obstacles;
    [SerializeField, FormerlySerializedAs("spawnOutsideDistance")] private float _spawnOutsideDistance = 5f;
    [SerializeField, FormerlySerializedAs("ranOffsetYRange")] private float _randomOffsetYRange = 0.5f;

    [SerializeField, FormerlySerializedAs("ranOffsetXRangeMax")] private float _randomOffsetXRangeMax = 5f;
    [SerializeField, FormerlySerializedAs("ranOffsetXRangeMin")] private float _randomOffsetXRangeMin = 2f;


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
                Random.Range(_randomOffsetXRangeMin, _randomOffsetXRangeMax),
                Random.Range(-_randomOffsetYRange, _randomOffsetYRange));

            SpawnPlatform(nextPosition);
        }
    }

    private void SpawnPlatform(Vector3 startPosition)
    {
        GameObject prefab = _buildings[Random.Range(0, _buildings.Length)];
        PlatBuilding building = Instantiate(prefab, transform).GetComponent<PlatBuilding>();

        // 프리팹마다 피벗 위치가 달라도 StartPoint끼리 이어지도록 보정한다.
        Vector3 startPointOffset = building.StartPoint.position - building.transform.position;
        building.transform.position = startPosition - startPointOffset;

        SpawnObstacles(building);

        _lastBuilding = building;
    }

    private void SpawnObstacles(PlatBuilding building)
    {
        if (_obstacles == null || _obstacles.Length == 0 || building.ObstaclePoints == null)
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
            int obstacleIndex = Random.Range(0, _obstacles.Length + 1);

            if (obstacleIndex == _obstacles.Length || _obstacles[obstacleIndex] == null)
            {
                continue;
            }

            Instantiate(_obstacles[obstacleIndex], obstaclePoint.position, obstaclePoint.rotation);
        }
    }


    private float GetSpawnX()
    {
        return _mainCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, -_mainCamera.transform.position.z)).x + _spawnOutsideDistance;
    }

}
