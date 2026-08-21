using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class Player : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("startSpeed")] private float _startSpeed;
    [SerializeField, FormerlySerializedAs("jumpForce")] private float _jumpForce = 10f;
    [SerializeField, FormerlySerializedAs("jumpHoldForce")] private float _jumpHoldForce = 15f;
    [SerializeField, FormerlySerializedAs("maxJumpHoldTime"), Min(0f)] private float _maxJumpHoldTime = 0.5f;
    [SerializeField, FormerlySerializedAs("comboCount")] private int _comboCount;
    [SerializeField] private float _comboSpeedIncreaseRate = 0.01f;
    [Header("Hit Stop")]
    [SerializeField, FormerlySerializedAs("hitStopDuration"), Min(0f)] private float _hitStopDuration = 0.05f;
    [Header("Game Over")]
    [SerializeField, FormerlySerializedAs("maxAirborneTime"), Min(0f)] private float _maxAirborneTime = 5f;
    [SerializeField, FormerlySerializedAs("gameOverPanel")] private GameObject _gameOverPanel;
    [Header("Attack")]
    [SerializeField, FormerlySerializedAs("slashObject")] private GameObject _slashObject;
    [SerializeField, FormerlySerializedAs("slashHitbox")] private BoxCollider2D _slashHitbox;
    [SerializeField, FormerlySerializedAs("attackDuration"), Min(0f)] private float _attackDuration = 0.15f;
    [Header("Ground Check")]
    [SerializeField, FormerlySerializedAs("groundCheckDistance")] private float _groundCheckDistance = 0.1f;
    [SerializeField, FormerlySerializedAs("minimumGroundNormalY"), Range(0f, 1f)] private float _minimumGroundNormalY = 0.7f;
    [SerializeField, FormerlySerializedAs("groundLayer")] private LayerMask _groundLayer;
    private Rigidbody2D _rigidbody;
    private Collider2D _playerCollider;
    private bool _isGrounded;
    private bool _isHoldingJump;
    private float _jumpHoldElapsed;
    private float _airborneElapsed;
    private Coroutine _attackCoroutine;
    private Coroutine _hitStopCoroutine;
    private bool _isGameOver;

    public bool IsSlashHitbox(Collider2D hitbox)
    {
        return hitbox == _slashHitbox;
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

        if (_slashHitbox != null)
        {
            _slashHitbox.enabled = false;
        }

        if (_slashObject != null)
        {
            _slashObject.SetActive(false);
        }

        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(false);
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

        Move();

        if (_isGrounded && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Jump();
        }

        if (Keyboard.current == null || !Keyboard.current.spaceKey.isPressed)
        {
            _isHoldingJump = false;
        }

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            Attack();
        }
    }

    private void Move()
    {
        _rigidbody.linearVelocityX = _startSpeed * (1 + _comboSpeedIncreaseRate * _comboCount);
    }

    private void FixedUpdate()
    {
        if (_isGameOver)
        {
            return;
        }

        ApplyJumpHoldForce();
        _isGrounded = IsGrounded();

        if (_isGrounded)
        {
            _airborneElapsed = 0f;
            return;
        }

        _airborneElapsed += Time.fixedDeltaTime;

        if (_airborneElapsed >= _maxAirborneTime)
        {
            GameOver();
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
            _playerCollider.bounds.extents.y + _groundCheckDistance,
            _groundLayer);

        return groundHit.collider != null && groundHit.normal.y >= _minimumGroundNormalY;
    }

    private void Jump()
    {
        _rigidbody.linearVelocityY = _jumpForce;
        _isGrounded = false;
        _isHoldingJump = true;
        _jumpHoldElapsed = 0f;
    }

    private void ApplyJumpHoldForce()
    {
        if (!_isHoldingJump || _jumpHoldElapsed >= _maxJumpHoldTime || _rigidbody.linearVelocityY <= 0f)
        {
            _isHoldingJump = false;
            return;
        }

        _rigidbody.AddForce(Vector2.up * _jumpHoldForce, ForceMode2D.Force);
        _jumpHoldElapsed += Time.fixedDeltaTime;
    }

    private void Attack()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
        }

        if (_slashObject != null)
        {
            _slashObject.SetActive(true);
        }

        if (_slashHitbox != null)
        {
            _slashHitbox.enabled = true;
        }

        _attackCoroutine = StartCoroutine(DisableAttackAfterDelay());
    }

    private IEnumerator DisableAttackAfterDelay()
    {
        yield return new WaitForSeconds(_attackDuration);

        if (_slashHitbox != null)
        {
            _slashHitbox.enabled = false;
        }

        if (_slashObject != null)
        {
            _slashObject.SetActive(false);
        }

        _attackCoroutine = null;
    }

    private IEnumerator ApplyHitStop()
    {
        Time.timeScale = 0f;
        // 일반 대기는 timeScale 0에서 끝나지 않으므로 실제 시간 기준으로 히트스톱을 해제한다.
        yield return new WaitForSecondsRealtime(_hitStopDuration);

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

        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(true);
        }
        else
        {
            Debug.Log("Game Over - Press R to restart.");
        }
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
