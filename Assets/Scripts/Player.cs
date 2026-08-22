using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }
    
    [SerializeField, Min(0f)] private float startSpeed = 10f;
    [SerializeField, Min(0f)] private float jumpForce = 20f;
    [Header("Variable Jump")]
    [SerializeField, Range(0f, 1f)] private float jumpReleaseVelocityMultiplier = 0.5f;
    [SerializeField] private int comboCount;
    [SerializeField] private float comboSpeedIncreaseRate = 0.01f;
    private GameObject _comboObject;
    private Animator _comboAnimator;
    private TMP_Text _comboText;
    [Header("Hit Stop")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.05f;
    [SerializeField, Min(0f)] private float hitStopDelay = 0.1f;
    [Header("Attack Timing")]
    [SerializeField, Min(0f)] private float attack12Interval = 0.2f;
    [SerializeField, Min(0f)] private float afterAttackInterval = 1f;
    [Header("Game Over")]
    [SerializeField, Min(0f)] private float maxAirborneTime = 2f;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip parrySFX;
    [SerializeField] private AudioClip hitSFX1;
    [SerializeField] private AudioClip hitSFX2;
    [SerializeField] private AudioClip hitSFX3;

    [SerializeField, Min(0f)]
    private float respawnTime = 1f;
    [SerializeField, Min(0f)] private float respawnDropHeight = 3f;
    [SerializeField] private PlatformManager platformManager;
    [SerializeField] private GameObject gameOverPanel;
    [Header("Level Complete")]
    [SerializeField] private GameObject endSet;
    [SerializeField, Min(0.01f)] private float endSlowMotionDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float endSlowTimeScale = 0.1f;
    [Header("Attack")]
    [SerializeField] private GameObject slashObject;
    [SerializeField] private GameObject slash1Object;
    [SerializeField] private GameObject slash2Object;
    [SerializeField] private BoxCollider2D slashHitbox;
    [SerializeField, Min(0f)] private float attackDuration = 0.25f;
    [SerializeField, Min(0f)] private float attackHitboxDuration = 0.1f;
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
    private int _FXCount;
    private float _airborneElapsed;
    private float _respawnElapsed;
    private Coroutine _attackCoroutine;
    private Coroutine _hitStopCoroutine;
    private bool _isHitStopped;
    private Sniper _sniper;
    private readonly HashSet<int> _activeParryWindows = new HashSet<int>();
    private int _nextParryWindowId;
    private bool _isGameOver;
    private bool _isLevelComplete;
    public bool IsRespawning { get; private set; }
    public bool IsRecoveringFromStumble { get; private set; }
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
        comboCount++;
        ShowCombo();
        _FXCount++;
        
        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
            _hitStopCoroutine = null;

            // 이전 히트 스톱 도중 새 타격이 들어오면 먼저 시간을 정상으로 되돌린다.
            if (_isHitStopped && !_isGameOver)
            {
                Time.timeScale = 1f;
                _isHitStopped = false;
            }
        }
        
        if (audioSource != null) {
            if (_FXCount % 3 == 1 && hitSFX1 != null)
            {
                audioSource.PlayOneShot(hitSFX1);
            }
            if (_FXCount % 3 == 2 && hitSFX2 != null)
            {
                audioSource.PlayOneShot(hitSFX2);
            }
            if (_FXCount % 3 == 0 && hitSFX3 != null)
            {
                audioSource.PlayOneShot(hitSFX3);
            }
        }
        
        _hitStopCoroutine = StartCoroutine(ApplyHitStop());
    }

    // 장애물 등에 부딪혔을 때 호출한다. Any State -> Stumble 전환을 사용한다.
    public void NotifyStumble()
    {
        ResetCombo();
        if (anim != null)
        {
            anim.SetTrigger(IsStumbleHash);
        }
    }

    // Obstacle의 일반 물리 충돌에서만 호출된다. 검 판정은 Trigger이므로 이 경로를 타지 않는다.
    public void NotifyObstacleCollision()
    {
        if (_isGameOver || IsRespawning || _rigidbody == null)
        {
            return;
        }

        IsRecoveringFromStumble = true;
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

        FindSlashEffectObjects();
        SetSlashEffectActive(null);
        FindComboUI();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (endSet == null)
        {
            endSet = GameObject.Find("EndSet");
        }

        if (endSet != null)
        {
            endSet.SetActive(false);
        }

        _FXCount = 0;
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

        if (_isLevelComplete)
        {
            return;
        }

        if (IsRespawning)
        {
            // 리스폰 중에는 조작 불가능한 상태에 놓인다.
            return;
        }

        Move();

        if (_isGrounded && Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
        {
            Jump();
        }

        // 상승 중 키를 놓는 시점에 따라 점프 높이를 낮춘다.
        if (Keyboard.current != null && Keyboard.current.zKey.wasReleasedThisFrame && _rigidbody.linearVelocityY > 0f)
        {
            _rigidbody.linearVelocityY *= jumpReleaseVelocityMultiplier;
        }

        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            Attack();
        }
    }
    
    private void Move()
    {
        if (IsRecoveringFromStumble && _stumbleElapsed < stumbleKnockbackDuration)
        {
            // 넉백 중에는 이동 속도로 덮어쓰지 않아 실제로 뒤로 밀려난다.
            return;
        }

        float recoveryMultiplier = GetStumbleRecoveryMultiplier();
        _rigidbody.linearVelocityX = startSpeed * (1 + comboSpeedIncreaseRate * comboCount) * recoveryMultiplier;
    }

    private void FixedUpdate()
    {
        if (_isGameOver || _isLevelComplete)
        {
            return;
        }

        if (IsRespawning)
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

        SetSlashEffectActive(attackType);

        if (slashHitbox != null)
        {
            slashHitbox.enabled = true;
        }

        if (_activeParryWindows.Count > 0)
        {
            _activeParryWindows.Clear();

            if (audioSource != null && parrySFX != null)
            {
                audioSource.PlayOneShot(parrySFX, 0.35f);
            }
        }

        _attackCoroutine = StartCoroutine(DisableAttackAfterDelay());
    }

    private IEnumerator DisableAttackAfterDelay()
    {
        // 판정은 공격 연출 전체보다 짧게 유지해 한 번의 휘두름만 맞게 한다.
        yield return new WaitForSecondsRealtime(Mathf.Min(attackHitboxDuration, attackDuration));

        if (slashHitbox != null)
        {
            slashHitbox.enabled = false;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, attackDuration - attackHitboxDuration));

        SetSlashEffectActive(null);
        _attackCoroutine = null;
    }

    private void FindSlashEffectObjects()
    {
        // Inspector 연결을 우선하고, 아직 연결하지 않았다면 Player 자식의 이름으로 찾는다.
        if (slash1Object == null)
        {
            Transform slash1 = transform.Find("Slash1");
            if (slash1 != null)
            {
                slash1Object = slash1.gameObject;
            }
        }

        if (slash2Object == null)
        {
            Transform slash2 = transform.Find("Slash2");
            if (slash2 != null)
            {
                slash2Object = slash2.gameObject;
            }
        }
    }

    private void FindComboUI()
    {
        _comboObject = GameObject.Find("combo");
        if (_comboObject == null)
        {
            return;
        }

        _comboAnimator = _comboObject.GetComponent<Animator>();
        _comboText = _comboObject.GetComponentInChildren<TMP_Text>(true);

        // 시작 화면에서 기본 상태 애니메이션이 한 번 보이지 않도록 숨긴다.
        _comboObject.SetActive(false);
    }

    private void ShowCombo()
    {
        if (_comboObject == null)
        {
            return;
        }

        if (_comboText != null)
        {
            _comboText.text = comboCount.ToString();
        }

        _comboObject.SetActive(true);
        if (_comboAnimator != null)
        {
            _comboAnimator.Play("combo_Appear", 0, 0f);
        }
    }

    private void ResetCombo()
    {
        comboCount = 0;

        if (_comboObject != null)
        {
            _comboObject.SetActive(false);
        }
    }

    private void SetSlashEffectActive(AttackType? attackType)
    {
        bool hasSeparateEffects = slash1Object != null || slash2Object != null;
        if (!hasSeparateEffects)
        {
            if (slashObject != null)
            {
                slashObject.SetActive(attackType.HasValue);
            }

            return;
        }

        if (slash1Object != null)
        {
            slash1Object.SetActive(attackType == AttackType.Attack1);
        }

        if (slash2Object != null)
        {
            slash2Object.SetActive(attackType == AttackType.Attack2);
        }

        // 기존 공용 이펙트가 별도 오브젝트가 아니라면 함께 보이지 않도록 숨긴다.
        if (slashObject != null && slashObject != slash1Object && slashObject != slash2Object)
        {
            slashObject.SetActive(false);
        }
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
        if (!IsRecoveringFromStumble)
        {
            return;
        }

        _stumbleElapsed += Time.fixedDeltaTime;
        if (_stumbleElapsed >= stumbleKnockbackDuration + stumbleRecoveryDuration)
        {
            IsRecoveringFromStumble = false;
        }
    }

    private float GetStumbleRecoveryMultiplier()
    {
        if (!IsRecoveringFromStumble)
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
        // 검이 물체에 닿은 뒤의 타격감을 위해 잠시 후 히트 스톱을 시작한다.
        yield return new WaitForSecondsRealtime(hitStopDelay);

        Time.timeScale = 0f;
        _isHitStopped = true;
        // 일반 대기는 timeScale 0에서 끝나지 않으므로 실제 시간 기준으로 히트스톱을 해제한다.
        yield return new WaitForSecondsRealtime(hitStopDuration);

        if (!_isGameOver)
        {
            Time.timeScale = 1f;
        }

        _isHitStopped = false;
        _hitStopCoroutine = null;
    }

    public void NotifyLevelComplete()
    {
        if (_isGameOver || _isLevelComplete)
        {
            return;
        }

        _isLevelComplete = true;

        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
            _hitStopCoroutine = null;
        }

        _isHitStopped = false;
        Time.timeScale = 1f;

        if (slashHitbox != null)
        {
            slashHitbox.enabled = false;
        }

        SetSlashEffectActive(null);
        StartCoroutine(LevelCompleteRoutine());
    }

    private IEnumerator LevelCompleteRoutine()
    {
        for (float elapsed = 0f; elapsed < endSlowMotionDuration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / endSlowMotionDuration);
            Time.timeScale = Mathf.Lerp(1f, endSlowTimeScale, progress);
            yield return null;
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero;
        }

        Time.timeScale = 0f;

        if (endSet != null)
        {
            endSet.SetActive(true);
        }
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
        if (IsRespawning)
        {
            return;
        }

        IsRespawning = true;
        _respawnElapsed = 0f;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        ResetCombo();
        _rigidbody.linearVelocity = Vector2.zero;

        // 추락 후 리스폰 위치에서 잠시 멈춰 있는 동안에는 Stumble을 보여준다.
        if (anim != null)
        {
            anim.SetBool(IsJumpingHash, false);
            anim.SetBool(IsFallingHash, false);
            anim.SetTrigger(IsStumbleHash);
        }

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
        IsRespawning = false;
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
