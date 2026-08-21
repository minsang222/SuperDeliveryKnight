using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public class Player : MonoBehaviour
{
    [SerializeField] private float startSpeed;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float jumpHoldForce = 15f;
    [SerializeField, Min(0f)] private float maxJumpHoldTime = 0.5f;
    [SerializeField] private int comboCount;
    [Header("Camera")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.05f;
    [SerializeField] private Vector3 cameraFollowOffset = new Vector3(2f, 0f, -10f);
    [Header("Game Over")]
    [SerializeField, Min(0f)] private float maxAirborneTime = 5f;
    [SerializeField] private GameObject gameOverPanel;
    [Header("Attack")]
    [SerializeField] private GameObject slashObject;
    [SerializeField] private BoxCollider2D slashHitbox;
    [SerializeField, Min(0f)] private float attackDuration = 0.15f;
    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.7f;
    [SerializeField] private LayerMask groundLayer;
    private Rigidbody2D rigid;
    private Collider2D playerCollider;
    private bool isGrounded;
    private bool isHoldingJump;
    private float jumpHoldElapsed;
    private float airborneElapsed;
    private Coroutine _attackCoroutine;
    private Coroutine _hitStopCoroutine;
    private CinemachineCamera _playerFollowCamera;
    private bool _isGameOver;

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
        Time.timeScale = 1f;
        rigid = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        SetupCinemachineCamera();

        if (slashHitbox == null)
        {
            BoxCollider2D[] boxColliders = GetComponents<BoxCollider2D>();
            if (boxColliders.Length > 1)
            {
                slashHitbox = boxColliders[1];
            }
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

        Move();

        if (isGrounded && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Jump();
        }

        if (Keyboard.current == null || !Keyboard.current.spaceKey.isPressed)
        {
            isHoldingJump = false;
        }

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            Attack();
        }
    }

    private void Move()
    {
        rigid.linearVelocityX = startSpeed * (1 + 0.01f * comboCount);
    }

    private void FixedUpdate()
    {
        if (_isGameOver)
        {
            return;
        }

        ApplyJumpHoldForce();
        isGrounded = IsGrounded();

        if (isGrounded)
        {
            airborneElapsed = 0f;
            return;
        }

        airborneElapsed += Time.fixedDeltaTime;

        if (airborneElapsed >= maxAirborneTime)
        {
            GameOver();
        }
    }

    private bool IsGrounded()
    {
        if (rigid.linearVelocityY > 0f || playerCollider == null)
        {
            return false;
        }

        RaycastHit2D groundHit = Physics2D.Raycast(
            playerCollider.bounds.center,
            Vector2.down,
            playerCollider.bounds.extents.y + groundCheckDistance,
            groundLayer);

        return groundHit.collider != null && groundHit.normal.y >= minimumGroundNormalY;
    }

    private void Jump()
    {
        rigid.linearVelocityY = jumpForce;
        isGrounded = false;
        isHoldingJump = true;
        jumpHoldElapsed = 0f;
    }

    private void ApplyJumpHoldForce()
    {
        if (!isHoldingJump || jumpHoldElapsed >= maxJumpHoldTime || rigid.linearVelocityY <= 0f)
        {
            isHoldingJump = false;
            return;
        }

        rigid.AddForce(Vector2.up * jumpHoldForce, ForceMode2D.Force);
        jumpHoldElapsed += Time.fixedDeltaTime;
    }

    private void Attack()
    {
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

        _attackCoroutine = StartCoroutine(DisableAttackAfterDelay());
    }

    private IEnumerator DisableAttackAfterDelay()
    {
        yield return new WaitForSeconds(attackDuration);

        if (slashHitbox != null)
        {
            slashHitbox.enabled = false;
        }

        if (slashObject != null)
        {
            slashObject.SetActive(false);
        }

        _attackCoroutine = null;
    }

    private void SetupCinemachineCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return;
        }

        if (mainCamera.GetComponent<CinemachineBrain>() == null)
        {
            mainCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        CreatePlayerFollowCamera(mainCamera);
    }

    private void CreatePlayerFollowCamera(Camera mainCamera)
    {
        if (_playerFollowCamera != null)
        {
            return;
        }

        GameObject followCameraObject = new GameObject("Player Follow Camera");
        _playerFollowCamera = followCameraObject.AddComponent<CinemachineCamera>();
        _playerFollowCamera.Lens = LensSettings.FromCamera(mainCamera);
        _playerFollowCamera.Follow = transform;

        CinemachineFollow follow = followCameraObject.AddComponent<CinemachineFollow>();
        follow.FollowOffset = cameraFollowOffset;

        _playerFollowCamera.ForceCameraPosition(mainCamera.transform.position, mainCamera.transform.rotation);
    }

    private IEnumerator ApplyHitStop()
    {
        Time.timeScale = 0f;
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

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
