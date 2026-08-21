using UnityEngine;
using UnityEngine.Serialization;

public class PlatBuilding : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("StartPoint")] private Transform _startPoint;
    [SerializeField, FormerlySerializedAs("EndPoint")] private Transform _endPoint;
    [SerializeField, FormerlySerializedAs("ObstaclePoints")] private Transform[] _obstaclePoints;
    [SerializeField, FormerlySerializedAs("destroyOutsideDistance"), Min(0f)] private float _destroyOutsideDistance = 5f;

    private Camera _mainCamera;

    public Transform StartPoint => _startPoint;
    public Transform EndPoint => _endPoint;
    public Transform[] ObstaclePoints => _obstaclePoints;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (_mainCamera == null || _endPoint == null)
        {
            return;
        }

        // 카메라가 플레이어를 따라가므로 고정 월드 좌표가 아니라 현재 화면 왼쪽을 폐기 기준으로 삼는다.
        float leftScreenX = _mainCamera.ViewportToWorldPoint(
            new Vector3(0f, 0.5f, -_mainCamera.transform.position.z)).x;

        if (_endPoint.position.x < leftScreenX - _destroyOutsideDistance)
        {
            Destroy(gameObject);
        }
    }
}
