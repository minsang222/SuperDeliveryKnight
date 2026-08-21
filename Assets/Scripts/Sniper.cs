using System;
using UnityEngine;

public class Sniper : MonoBehaviour
{
    public static Sniper Instance { get; private set; }
    
    // 보고 반응하기 거의 불가능한 윈도우여야 하므로, 0.2초 이하를 권장한다.
    [SerializeField, Min(0f)] private float parryableWindow = 0.1f;
    [SerializeField, Min(0f)] private double thresholdChance = 0.6f;
    [SerializeField, Min(0f)] private double thresholdChanceOfDouble = 0.16f;
    
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip warningSFX;
    public event Action<float, float> HasAimed;
    private const float AimToShot = 0.8333f;
    private bool _isDouble;
    private int _nextShot;
    private Clock _clock;
    private System.Random _myDefaultPositionRandomSeed;

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
                return;
            }

            // 패리 코루틴 플레이어에서 실행
            HasAimed?.Invoke(AimToShot, parryableWindow);
            if (_isDouble)
            {
                _isDouble = false;
                _nextShot = 3;
                return;
            }
            // 애니메이션 재생
            // 효과음 재생
            if (audioSource != null && warningSFX != null)
            {
                audioSource.PlayOneShot(warningSFX);
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
            // TODO: 다음 뒷배경 청크 생성
        }
    }
    
    private void StartAim()
    {
        // 조준 레이저 애니메이션
        // TODO: if _isDouble
    }
}
