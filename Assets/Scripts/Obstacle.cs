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
