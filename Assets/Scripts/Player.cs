using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class Player : MonoBehaviour
{
    [SerializeField, Min(0f)] private float startSpeed = 10f;
    [SerializeField, Min(0f)] private float jumpForce = 20f;
    [SerializeField, Min(0f)] private float jumpHoldForce = 20f;
    [SerializeField, Min(0f)] private float maxJumpHoldTime = 0.5f;
    [SerializeField] private int comboCount;
    [SerializeField] private float comboSpeedIncreaseRate = 0.01f;
    [Header("Hit Stop")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.05f;
    [Header("Game Over")]
    [SerializeField, Min(0f)] private float maxAirborneTime = 2.5f;

    [SerializeField, Min(0f)]
    private float respawnTime = 2f;
    [SerializeField] private GameObject gameOverPanel;
    [Header("Attack")]
    [SerializeField] private GameObject slashObject;
    [SerializeField] private BoxCollider2D slashHitbox;
    [SerializeField, Min(0f)] private float attackDuration = 0.15f;

    [SerializeField, Min(0f)]
    private float attackCooldown = 0.25f;
    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.7f;
    [SerializeField] private LayerMask groundLayer;
    private Rigidbody2D _rigidbody;
    private Collider2D _playerCollider;
    private bool _isGrounded;
    private bool _isHoldingJump;
    private float _jumpHoldElapsed;
    private float _airborneElapsed;
    private float _respawnElapsed;
    private Coroutine _attackCoroutine;
    private Coroutine _hitStopCoroutine;
    private bool _isGameOver;
    private bool _isRespawning;

    public bool IsSlashHitbox(Collider2D hitbox)
    {
        return hitbox == slashHitbox;
    }

    public void NotifySlashHit()
    {
        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
        }

        _hitStopCoroutine = StartCoroutine(ApplyHitStop());
    }

    private void Awake()
    {
        // 게임 오버나 히트스톱 중 재시작되어도 새 씬은 정상 시간으로 출발해야 한다.
        Time.timeScale = 1f;
        _rigidbody = GetComponent<Rigidbody2D>();
        _playerCollider = GetComponent<Collider2D>();

        if (_rigidbody == null)
        {
            Console.Write("critical error");
        }

        if (slashHitbox != null)
        {
            slashHitbox.enabled = false;
        }

        if (slashObject != null)
        {
            slashObject.SetActive(false);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }
    void Update()
    {
        if (_isGameOver)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartGame();
            }

            return;
        }

        if (_isRespawning)
        {
            // 리스폰 중에는 조작 불가능한 상태에 놓인다.
            return;
        }

        Move();

        if (_isGrounded && Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
        {
            Jump();
        }

        if (Keyboard.current == null || !Keyboard.current.zKey.isPressed)
        {
            _isHoldingJump = false;
        }

        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            Attack();
        }
    }
    
    private void Move()
    {
        _rigidbody.linearVelocityX = startSpeed * (1 + comboSpeedIncreaseRate * comboCount);
    }

    private void FixedUpdate()
    {
        if (_isGameOver)
        {
            return;
        }

        if (_isRespawning)
        {
            _respawnElapsed += Time.fixedDeltaTime;
            if (_respawnElapsed >= respawnTime)
            {
                _isRespawning = false;
                // respawn coroutine 종료
            }
        }

        // ApplyJumpHoldForce();
        _isGrounded = IsGrounded();

        if (_isGrounded)
        {
            _airborneElapsed = 0f;
            return;
        }

        _airborneElapsed += Time.fixedDeltaTime;

        if (_airborneElapsed >= maxAirborneTime)
        {
            Respawn();
        }
    }

    private bool IsGrounded()
    {
        // 점프 직후 발밑 레이가 여전히 바닥을 잡아 공중 점프를 허용하지 않도록 상승 중에는 검사하지 않는다.
        if (_rigidbody.linearVelocityY > 0f || _playerCollider == null)
        {
            return false;
        }

        RaycastHit2D groundHit = Physics2D.Raycast(
            _playerCollider.bounds.center,
            Vector2.down,
            _playerCollider.bounds.extents.y + groundCheckDistance,
            groundLayer);

        return groundHit.collider != null && groundHit.normal.y >= minimumGroundNormalY;
    }

    private void Jump()
    {
        _rigidbody.linearVelocityY = jumpForce;
        _rigidbody.AddForce(Vector2.up * jumpHoldForce, ForceMode2D.Force);
        _isGrounded = false;
        _isHoldingJump = true;
        _jumpHoldElapsed = 0f;
    }

    [Obsolete("점프 출력 고정",  true)]
    private void ApplyJumpHoldForce()
    {
        if (!_isHoldingJump || _jumpHoldElapsed >= maxJumpHoldTime || _rigidbody.linearVelocityY <= 0f)
        {
            _isHoldingJump = false;
            return;
        }

        _rigidbody.AddForce(Vector2.up * jumpHoldForce, ForceMode2D.Force);
        _jumpHoldElapsed += Time.fixedDeltaTime;
    }

    private void Attack()
    {
        if (_attackCoroutine != null)
        {
            // attackCooldown에 걸린 상태
            return;
        }

        if (slashObject != null)
        {
            slashObject.SetActive(true);
        }

        if (slashHitbox != null)
        {
            slashHitbox.enabled = true;
        }

        _attackCoroutine = StartCoroutine(DisableAttackAfterDelay());
    }

    private IEnumerator DisableAttackAfterDelay()
    {
        yield return new WaitForSeconds(Time.fixedDeltaTime);
        
        if (slashHitbox != null)
        {
            slashHitbox.enabled = false;
        }
        
        yield return new WaitForSeconds(attackDuration);

        if (slashObject != null)
        {
            slashObject.SetActive(false);
        }

        yield return new WaitForSeconds(attackCooldown);
        
        _attackCoroutine = null;
        
    }

    private IEnumerator ApplyHitStop()
    {
        Time.timeScale = 0f;
        // 일반 대기는 timeScale 0에서 끝나지 않으므로 실제 시간 기준으로 히트스톱을 해제한다.
        yield return new WaitForSecondsRealtime(hitStopDuration);

        if (!_isGameOver)
        {
            Time.timeScale = 1f;
        }

        _hitStopCoroutine = null;
    }

    private void GameOver()
    {
        _isGameOver = true;
        // 이동·물리·공격 연출을 함께 멈추는 게임잼용 단일 일시정지 지점이다.
        Time.timeScale = 0f;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        else
        {
            Debug.Log("Game Over - Press R to restart.");
        }
    }

    private void Respawn()
    {
        // TEMP, TODO: 다음 청크 계산해서 위에 소환
        _isGameOver = true;
        // 이동·물리·공격 연출을 함께 멈추는 게임잼용 단일 일시정지 지점이다.
        Time.timeScale = 0f;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
