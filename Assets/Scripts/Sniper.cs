using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class Sniper : MonoBehaviour
{
    public static Sniper Instance { get; private set; }
    [SerializeField] private TMP_Text parryGuideText;
    
    // 보고 반응하기 거의 불가능한 윈도우여야 하므로, 0.2초 이하를 권장한다.
    [SerializeField, Min(0f)] private float parryableWindow = 0.2f;
    [SerializeField, Min(0f)] private double thresholdChance = 0.2f;
    [SerializeField, Min(0f)] private double thresholdChanceOfDouble = 0.05f;
    
    private System.Random _myDefaultPositionRandomSeed;
    
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip warningSFX;
    [Header("Aim Ray")]
    [SerializeField] private Vector2 redRayViewportPoint = new Vector2(0.9f, 0.8f);
    [SerializeField, Min(0f)] private float initRayWidth = 0.05f;
    [SerializeField, Min(0f)] private float redRayWidth = 0.15f;
    [SerializeField] private Color initRayColor = new Color(212f / 255f, 164f / 255f, 155f / 255f);
    [SerializeField] private Color redRayColor = Color.red;
    [SerializeField] private AnimationCurve redRayAlphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public event Action<float, float> HasAimed;
    private const float AimToShot = 0.8333f;
    private bool _isDouble;
    private int _nextShot;
    private Clock _clock;
    // The sniper is intentionally scheduled only once after this position.
    private const float SniperSpawnX = 10f;
    private bool _hasScheduledHardcodedShot;
    [SerializeField] private LineRenderer redRay;
    private Coroutine redRayCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Sniper 중복");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        _nextShot = 0;
    }

    private void Start()
    {
        _clock = Clock.Instance;
        if (_clock != null)
        {
            _clock.Heartbeat += OnHeartbeat;
        }
        
        _myDefaultPositionRandomSeed = new System.Random(
            PlatformManager.Instance != null ? PlatformManager.Instance.DefaultPositionRandomSeed : 0);
        
        parryGuideText.enabled = false;
    }

    private void OnDestroy()
    {
        if (_clock != null)
        {
            _clock.Heartbeat -= OnHeartbeat;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnHeartbeat(int elapsedTime)
    {
        if (_nextShot > 0)
        {
            _nextShot--;
            if (_nextShot != 2)
            {
                if (_nextShot == 0)
                {
                    // 스나이퍼 네모 게임오브젝트 표시 off
                }
                return;
            }

            // 패리 코루틴 플레이어에서 실행
            StartAim();
            if (_hasScheduledHardcodedShot) StartCoroutine(FireFirstShot());
            HasAimed?.Invoke(AimToShot, parryableWindow);
            if (_isDouble)
            {
                _isDouble = false;
                _nextShot = 3;
                
                if (audioSource != null && warningSFX != null)
                {
                    audioSource.PlayOneShot(warningSFX, 0.5f);
                }
                return;
            }
            // 애니메이션 재생
            // 효과음 재생
            if (audioSource != null && warningSFX != null)
            {
                audioSource.PlayOneShot(warningSFX, 0.5f);
            }
            return;
        }
        
        // TODO: (정식 버전에서는 오브젝트 스트림을 중앙에서 발행. 게임잼에서는 배제)
        var res = _myDefaultPositionRandomSeed.NextDouble();
        if (res < thresholdChanceOfDouble)
        {
            _nextShot = 4;
            _isDouble = true;
            return;
        }

        if (res < thresholdChance)
        {
            _nextShot = 4;
            return;
            // TODO: 다음 뒷배경 청크 생성
        }
        
        if (_hasScheduledHardcodedShot || Player.Instance == null ||
            Player.Instance.transform.position.x < SniperSpawnX)
        {
            return;
        }

        _hasScheduledHardcodedShot = true;
        _isDouble = false;
        _nextShot = 4;
        // 스나이퍼 네모 게임오브젝트 표시 on
        // 레이캐스트 코루틴 시작
    }

    private IEnumerator FireFirstShot()
    {
        yield return new WaitForSecondsRealtime(AimToShot - (parryableWindow / 2f));
        parryGuideText.enabled = true;
        
        yield return new WaitForSecondsRealtime(parryableWindow);
        parryGuideText.enabled = false;
    }

    private IEnumerator RedRaycast()
    {
        Camera mainCamera = Camera.main;
        if (Player.Instance == null || mainCamera == null)
        {
            yield break;
        }

        if (redRay == null)
        {
            redRay = GetComponent<LineRenderer>();
            if (redRay == null)
            {
                redRay = gameObject.AddComponent<LineRenderer>();
            }
        }

        redRay.positionCount = 2;
        redRay.useWorldSpace = true;
        redRay.startWidth = initRayWidth;
        redRay.endWidth = initRayWidth;
        redRay.enabled = true;
        redRay.sortingOrder = 10;

        float duration = RedRayDuration(AimToShot, parryableWindow);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            mainCamera = Camera.main;
            if (Player.Instance == null || mainCamera == null)
            {
                break;
            }

            Vector3 start = mainCamera.ViewportToWorldPoint(new Vector3(
                redRayViewportPoint.x, redRayViewportPoint.y, -mainCamera.transform.position.z));
            start.z = 0f;
            Vector3 end = Player.Instance.transform.position;
            end.z = 0f;
            redRay.SetPosition(0, start);
            redRay.SetPosition(1, end);

            Color color = RayColor(initRayColor, redRayColor,
                Mathf.Clamp01(redRayAlphaCurve.Evaluate(elapsed / duration)));
            redRay.startColor = color;
            redRay.endColor = color;
            redRay.endWidth = Mathf.Lerp(initRayWidth, redRayWidth, elapsed / duration);
            yield return null;
        }
        
        redRay.enabled = false;
    }

    private static float RedRayDuration(float aimToShot, float parryWindow)
    {
        return Mathf.Max(0f, aimToShot - parryWindow / 2f);
    }

    private static Color RayColor(Color initial, Color target, float progress)
    {
        return Color.Lerp(initial, target, progress);
    }
    
    private void StartAim()
    {
        if (redRayCoroutine != null)
        {
            StopCoroutine(redRayCoroutine);
        }

        redRayCoroutine = StartCoroutine(RedRaycast());
        // TODO: if _isDouble
    }
}
