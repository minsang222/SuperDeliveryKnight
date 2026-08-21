using System;
using UnityEngine;

public class Clock : MonoBehaviour
{
    public static Clock Instance { get; private set; }
    public event Action<int> Heartbeat;
    
    private int _timeElapsed;
    private float _fTimeElapsed;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Clock 중복");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 스테이지 시작 시, 클락 시작과 함께 시간 초기화
        _timeElapsed = 0;
        _fTimeElapsed = 0.001f;
    }

    void Update()
    {
        // Clock 구현을 위한 고정 델타프레임.
        var tmp = _timeElapsed;
        _fTimeElapsed += Time.unscaledDeltaTime;
        _timeElapsed = Mathf.FloorToInt(_fTimeElapsed * 720f);
        if (Mathf.FloorToInt(tmp / 300) < Mathf.FloorToInt(_timeElapsed / 300))
        {
            Heartbeat?.Invoke(_timeElapsed);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}