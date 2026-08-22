using System;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public enum Type
    {
        Hanging,
        Still
    }

    [SerializeField] private Type type;
    [SerializeField] private Vector2 slashKnockback = new Vector2(12f, 6f);
    [SerializeField, Min(0f)] private float destroyDelay = 2f;
    
    private Collider2D[] _colliders;
    private Rigidbody2D _rigidbody;
    private bool _isDestroyed;
    private const float LandingContactTolerance = 0.05f;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider2D>();
        _rigidbody = GetComponent<Rigidbody2D>();

        // 매달린 장애물은 파괴되기 전까지 현재 위치에 고정한다.
        if (type == Type.Hanging && _rigidbody != null)
        {
            _rigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDestroyBySlash(other);
    }

    // 검 히트박스가 이미 겹친 채 활성화된 경우 Enter 콜백을 놓칠 수 있으므로
    // 겹쳐 있는 동안에도 한 번 더 확인한다.
    private void OnTriggerStay2D(Collider2D other)
    {
        TryDestroyBySlash(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_isDestroyed || TryDestroyBySlash(collision.collider))
        {
            return;
        }

        Player player = collision.collider.GetComponent<Player>();
        if (player != null)
        {
            if (IsPlayerLandingOnTop(collision))
            {
                // 위에서 밟은 경우에는 장애물을 발판처럼 유지한다.
                return;
            }

            player.NotifyObstacleCollision();
            _isDestroyed = true;
            DestroyObstacle();
        }
    }

    private static bool IsPlayerLandingOnTop(Collision2D collision)
    {
        if (collision.otherCollider == null)
        {
            return false;
        }

        float obstacleTop = collision.otherCollider.bounds.max.y;
        foreach (ContactPoint2D contact in collision.contacts)
        {
            bool isTopSurfaceContact = contact.point.y >= obstacleTop - LandingContactTolerance;
            bool isVerticalContact = Mathf.Abs(contact.normal.y) > 0.7f;
            if (isTopSurfaceContact && isVerticalContact)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryDestroyBySlash(Collider2D hitbox)
    {
        
        // 공격 판정이 여러 콜백으로 겹쳐도 히트스톱과 파괴 예약은 한 번만 실행한다.
        if (_isDestroyed)
        {
            return false;
        }

        Player player = hitbox.GetComponent<Player>();

        if (player == null || !player.IsSlashHitbox(hitbox))
        {
            return false;
        }

        _isDestroyed = true;
        player.NotifySlashHit();
        DestroyObstacle();
        return true;
    }

    private void DestroyObstacle()
    {
        foreach (Collider2D obstacleCollider in _colliders)
        {
            obstacleCollider.enabled = false;
        }

        if (_rigidbody != null)
        {
            if (type == Type.Still)
            {
                _rigidbody.linearVelocity = slashKnockback;
            }
            else if (type == Type.Hanging)
            {
                _rigidbody.constraints = RigidbodyConstraints2D.None;
                _rigidbody.WakeUp();
            }
        }

        Destroy(gameObject, destroyDelay);
    }
}
