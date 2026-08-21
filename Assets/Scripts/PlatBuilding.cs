using UnityEngine;
using UnityEngine.Serialization;

public class PlatBuilding : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("_startPoint"), FormerlySerializedAs("StartPoint")] private Transform startPoint;
    [SerializeField, FormerlySerializedAs("_endPoint"), FormerlySerializedAs("EndPoint")] private Transform endPoint;
    [SerializeField, FormerlySerializedAs("_obstaclePoints"), FormerlySerializedAs("ObstaclePoints")] private Transform[] obstaclePoints;
    [SerializeField, FormerlySerializedAs("_destroyOutsideDistance"), FormerlySerializedAs("destroyOutsideDistance"), Min(0f)]
    private float destroyOutsideDistance = 5f;

    private Camera _mainCamera;

    public Transform StartPoint => startPoint;
    public Transform EndPoint => endPoint;
    public Transform[] ObstaclePoints => obstaclePoints;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (_mainCamera == null || endPoint == null)
        {
            return;
        }

        // 카메라가 플레이어를 따라가므로 고정 월드 좌표가 아니라 현재 화면 왼쪽을 폐기 기준으로 삼는다.
        float leftScreenX = _mainCamera.ViewportToWorldPoint(
            new Vector3(0f, 0.5f, -_mainCamera.transform.position.z)).x;

        if (endPoint.position.x < leftScreenX - destroyOutsideDistance)
        {
            Destroy(gameObject);
        }
    }
}
