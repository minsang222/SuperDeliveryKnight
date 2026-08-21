using UnityEngine;

public class PlatBuilding : MonoBehaviour
{
    public Transform StartPoint;
    public Transform EndPoint;
    public Transform[] ObstaclePoints;
    [SerializeField, Min(0f)] private float destroyOutsideDistance = 5f;

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (_mainCamera == null || EndPoint == null)
        {
            return;
        }

        float leftScreenX = _mainCamera.ViewportToWorldPoint(
            new Vector3(0f, 0.5f, -_mainCamera.transform.position.z)).x;

        if (EndPoint.position.x < leftScreenX - destroyOutsideDistance)
        {
            Destroy(gameObject);
        }
    }
}
