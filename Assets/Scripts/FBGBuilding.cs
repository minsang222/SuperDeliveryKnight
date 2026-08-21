using UnityEngine;
using UnityEngine.Serialization;

public class FBGBuilding : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("ObstaclePoint")] private Transform[] obstaclePoints;
    [SerializeField, Min(0f)] private float destroyOutsideDistance = 5f;

    public Transform[] ObstaclePoints => obstaclePoints;

    private Camera _mainCamera;
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (_mainCamera == null || _spriteRenderer == null)
        {
            return;
        }

        float leftScreenX = _mainCamera.ViewportToWorldPoint(
            new Vector3(0f, 0.5f, -_mainCamera.transform.position.z)).x;

        if (_spriteRenderer.bounds.max.x < leftScreenX - destroyOutsideDistance)
        {
            Destroy(gameObject);
        }
    }
}
