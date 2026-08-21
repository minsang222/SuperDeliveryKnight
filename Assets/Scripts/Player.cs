using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }
    
    [SerializeField, Min(0f)] private float startSpeed = 10f;
    [SerializeField, Min(0f)] private float jumpForce = 20f;
    [SerializeField] private int comboCount;
    [SerializeField] private float comboSpeedIncreaseRate = 0.01f;
    [Header("Hit Stop")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.05f;
    [Header("Attack Timing")]
    [SerializeField, Min(0f)] private float attack12Interval = 0.2f;
    [SerializeField, Min(0f)] private float afterAttackInterval = 1f;
    [Header("Game Over")]
    [SerializeField, Min(0f)] private float maxAirborneTime = 2.5f;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip parrySFX;

    [SerializeField, Min(0f)]
    private float respawnTime = 2f;
    [SerializeField, Min(0f)] private float respawnDropHeight = 5f;
    [SerializeField] private PlatformManager platformManager;
    [SerializeField] private GameObject gameOverPanel;
    [Header("Attack")]
    [SerializeField] private GameObject slashObject;
    [SerializeField] private BoxCollider2D slashHitbox;
    [SerializeField, Min(0f)] private float attackDuration = 0.15f;

    [SerializeField, Min(0f)]
    private float attackCooldown = 0.4f;
    [Header("Stumble")]
    [SerializeField, Min(0f)] private float stumbleKnockbackSpeed = 4f;
    [SerializeField, Min(0f)] private float stumbleKnockbackDuration = 0.15f;
    [SerializeField, Range(0f, 1f)] private float stumbleStartSpeedMultiplier = 0.25f;
    [SerializeField, Min(0f)] private float stumbleRecoveryDuration = 4.5f;
    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField, UnityEngine.Range(0f, 1f)] private float minimumGroundNormalY = 0.7f;
    [SerializeField] private LayerMask groundLayer;
    private const float SafetyMargin = 5f;
    private static readonly int DoAttackHash = Animator.StringToHash("doAttack");
    private static readonly int IsAttack2Hash = Animator.StringToHash("isAttack2");
    private static readonly int IsFallingHash = Animator.StringToHash("isFalling");
    private static readonly int IsJumpingHash = Animator.StringToHash("isJumping");
    private static readonly int IsStumbleHash = Animator.StringToHash("isStumble");

    private Rigidbody2D _rigidbody;
    private Collider2D _playerCollider;
    [SerializeField] private Animator anim;
    private bool _isGrounded;
    private float _airborneElapsed;
    private float _respawnElapsed;
    private Coroutine _attackCoroutine;
    private Coroutine _hitStopCoroutine;
    private Sniper _sniper;
    private readonly HashSet<int> _activeParryWindows = new HashSet<int>();
    private int _nextParryWindowId;
    private bool _isGameOver;
    private bool _isRespawning;
    private bool _isRecoveringFromStumble;
    private float _stumbleElapsed;
    private AttackType _lastAttackType;
    private float _lastAttackTime = float.NegativeInfinity;

    private enum AttackType
    {
        None,
        Attack1,
        Attack2
    }

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

    // 장애물 등에 부딪혔을 때 호출한다. Any State -> Stumble 전환을 사용한다.
    public void NotifyStumble()
    {
        if (anim != null)
        {
            anim.SetTrigger(IsStumbleHash);
        }
    }

    // Obstacle의 일반 물리 충돌에서만 호출된다. 검 판정은 Trigger이므로 이 경로를 타지 않는다.
    public void NotifyObstacleCollision()
    {
        if (_isGameOver || _isRespawning || _rigidbody == null)
        {
            return;
        }

        _isRecoveringFromStumble = true;
        _stumbleElapsed = 0f;
        _rigidbody.linearVelocityX = -stumbleKnockbackSpeed;
        NotifyStumble();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("PlatformMananger 중복");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 게임 오버나 히트스톱 중 재시작되어도 새 씬은 정상 시간으로 출발해야 한다.
        Time.timeScale = 1f;
        _rigidbody = GetComponent<Rigidbody2D>();
        _playerCollider = GetComponent<Collider2D>();

        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

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

    private void Start()
    {
        _sniper = Sniper.Instance;
        if (_sniper != null)
        {
            _sniper.HasAimed += ReadyParry;
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

        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            Attack();
        }
    }
    
    private void Move()
    {
        if (_isRecoveringFromStumble && _stumbleElapsed < stumbleKnockbackDuration)
        {
            // 넉백 중에는 이동 속도로 덮어쓰지 않아 실제로 뒤로 밀려난다.
            return;
        }

        float recoveryMultiplier = GetStumbleRecoveryMultiplier();
        _rigidbody.linearVelocityX = startSpeed * (1 + comboSpeedIncreaseRate * comboCount) * recoveryMultiplier;
    }

    private void FixedUpdate()
    {
        if (_isGameOver)
        {
            return;
        }

        if (_isRespawning)
        {
            return;
        }

        UpdateStumbleRecovery();

        _isGrounded = IsGrounded();
        UpdateMovementAnimationParameters();

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
        _isGrounded = false;

        if (anim != null)
        {
            anim.SetBool(IsJumpingHash, true);
            anim.SetBool(IsFallingHash, false);
        }
    }

    private void Attack()
    {
        float elapsedSinceLastAttack = Time.time - _lastAttackTime;

        // Attack1 직후의 입력만 Attack2로 잇는다. 콤보 창이 끝난 뒤에는
        // 마지막 공격으로부터 afterAttackInterval이 지난 후에만 Attack1을 다시 허용한다.
        if (_lastAttackType == AttackType.Attack1 && elapsedSinceLastAttack < attack12Interval)
        {
            StartAttack(AttackType.Attack2);
            return;
        }

        if (elapsedSinceLastAttack < afterAttackInterval)
        {
            return;
        }

        StartAttack(AttackType.Attack1);
    }

    private void StartAttack(AttackType attackType)
    {
        _lastAttackType = attackType;
        _lastAttackTime = Time.time;

        if (anim != null)
        {
            if (attackType == AttackType.Attack1)
            {
                // Attack1의 분기 조건을 다음 콤보 입력 전까지 false로 유지한다.
                anim.SetBool(IsAttack2Hash, false);
                anim.SetTrigger(DoAttackHash);
            }
            else
            {
                anim.SetBool(IsAttack2Hash, true);
            }
        }

        // Attack2가 콤보 창 안에 들어오면 첫 공격의 연출 종료 코루틴을
        // 새 공격 기준으로 다시 시작한다.
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
        }

        if (slashObject != null)
        {
            slashObject.SetActive(true);
        }

        if (slashHitbox != null)
        {
            slashHitbox.enabled = true;
        }

        if (_activeParryWindows.Count > 0)
        {
            _activeParryWindows.Clear();

            if (audioSource != null && parrySFX != null)
            {
                audioSource.PlayOneShot(parrySFX);
            }
        }

        _attackCoroutine = StartCoroutine(DisableAttackAfterDelay());
    }

    private IEnumerator DisableAttackAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Time.fixedDeltaTime);
        
        if (slashHitbox != null)
        {
            slashHitbox.enabled = false;
        }
        
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, attackDuration - Time.fixedDeltaTime));

        if (slashObject != null)
        {
            slashObject.SetActive(false);
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, attackCooldown - attackDuration));
        
        _attackCoroutine = null;
    }

    private void UpdateMovementAnimationParameters()
    {
        if (anim == null || _rigidbody == null)
        {
            return;
        }

        float verticalVelocity = _rigidbody.linearVelocityY;
        anim.SetBool(IsJumpingHash, !_isGrounded && verticalVelocity > 0f);
        anim.SetBool(IsFallingHash, !_isGrounded && verticalVelocity < 0f);
    }

    private void UpdateStumbleRecovery()
    {
        if (!_isRecoveringFromStumble)
        {
            return;
        }

        _stumbleElapsed += Time.fixedDeltaTime;
        if (_stumbleElapsed >= stumbleKnockbackDuration + stumbleRecoveryDuration)
        {
            _isRecoveringFromStumble = false;
        }
    }

    private float GetStumbleRecoveryMultiplier()
    {
        if (!_isRecoveringFromStumble)
        {
            return 1f;
        }

        float recoveryElapsed = Mathf.Max(0f, _stumbleElapsed - stumbleKnockbackDuration);
        float recoveryProgress = stumbleRecoveryDuration <= 0f
            ? 1f
            : Mathf.Clamp01(recoveryElapsed / stumbleRecoveryDuration);

        return Mathf.Lerp(stumbleStartSpeedMultiplier, 1f, recoveryProgress);
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
        if (_isRespawning)
        {
            return;
        }

        _isRespawning = true;
        _respawnElapsed = 0f;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        comboCount = 0;
        _rigidbody.linearVelocity = Vector2.zero;

        if (platformManager != null &&
            platformManager.TryGetRespawnPoint(transform.position.x, respawnDropHeight, out Vector3 respawnPosition))
        {
            transform.position = respawnPosition;
        }
        else
        {
            transform.position += Vector3.up * respawnDropHeight;
        }

        while (_respawnElapsed < respawnTime)
        {
            yield return null;
            _respawnElapsed += Time.deltaTime;
        }

        _airborneElapsed = 0f;
        _isRespawning = false;
    }

    public void ReadyParry(float wait, float window)
    {
        StartCoroutine(ParryCoroutine(
            ++_nextParryWindowId,
            Mathf.Max(0f, wait - window / 2f),
            Mathf.Max(0f, window)));
    }

    private IEnumerator ParryCoroutine(int windowId, float delay, float window)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (window <= 0f)
        {
            ParryFailed();
            yield break;
        }

        _activeParryWindows.Add(windowId);
        yield return new WaitForSecondsRealtime(window);

        if (_activeParryWindows.Remove(windowId))
        {
            ParryFailed();
        }
    }

    private void ParryFailed()
    {
        // 피격 애니메이션
        // 피격 효과음
        // 피격 상태
    }
    
    // (연구 필요) 0.5f * 9.8f 부분은 Rigidbody 중력 물리가 예측 가능하게 동작해야 올바른 식이 된다.
    // 호출자에게 얼마만큼의 너비와 높이 여유가 있는지 정보를 전달해야 하므로, bool이 아닌 float로 설계한다.
    public Vector2 CanReachChunk(Transform start, Transform end)
    {
        var (x1, y1) = (start.position.x, start.position.y);
        var (x2, y2) = (end.position.x, end.position.y);

        float gravity = Mathf.Abs(Physics2D.gravity.y * _rigidbody.gravityScale);
        if (startSpeed <= 0f || gravity <= 0f)
        {
            return new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        }

        float timeLanding = (x2 - x1) / startSpeed;
        float heightLanding = y1 + jumpForce * timeLanding - 0.5f * gravity * timeLanding * timeLanding;

        float flightTime = 2f * jumpForce / gravity;
        float widthLanding = x1 + startSpeed * flightTime;

        return new Vector2(widthLanding - x2 - SafetyMargin, heightLanding - y2 - SafetyMargin);
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    private void OnDestroy()
    {
        if (_sniper != null)
        {
            _sniper.HasAimed -= ReadyParry;
        }

        if (Instance == this)
            Instance = null;
    }
}
