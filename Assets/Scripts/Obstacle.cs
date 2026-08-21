using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [SerializeField] private Vector2 slashKnockback = new Vector2(12f, 6f);
    [SerializeField, Min(0f)] private float destroyDelay = 2f;

    private Collider2D[] _colliders;
    private Rigidbody2D _rigidbody;
    private bool _isDestroyed;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider2D>();
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDestroyBySlash(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDestroyBySlash(collision.collider);
    }

    private void TryDestroyBySlash(Collider2D hitbox)
    {
        // 공격 판정이 여러 콜백으로 겹쳐도 히트스톱과 파괴 예약은 한 번만 실행한다.
        if (_isDestroyed)
        {
            return;
        }

        Player player = hitbox.GetComponent<Player>();

        if (player == null || !player.IsSlashHitbox(hitbox))
        {
            return;
        }

        _isDestroyed = true;
        player.NotifySlashHit();

        // 파괴 지연 동안 날아가는 연출은 남기되, 플레이어와의 추가 충돌은 막는다.
        foreach (Collider2D obstacleCollider in _colliders)
        {
            obstacleCollider.enabled = false;
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = slashKnockback;
        }

        Destroy(gameObject, destroyDelay);
    }
}
