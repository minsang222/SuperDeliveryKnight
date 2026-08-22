using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Timeline : MonoBehaviour
{
    public static Timeline Instance { get; private set; }
    [SerializeField] private TMP_Text currentTimeText;
    [SerializeField] private int timeLimit = 99;
    public int currentTime { get; private set; }
    
    [SerializeField] private Slider slider;
    [SerializeField] private float stageStartX = 0f;
    [SerializeField] private float stageEndX = 1000f;
    
    private Clock _clock;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Timeline 중복");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        
        _clock = Clock.Instance;
        if (_clock != null)
        {
            _clock.Heartbeat += OnHeartbeat;
        }

        currentTime = timeLimit;
    }

    // Update is called once per frame
    void Update()
    {
        if (Player.Instance == null) return;

        float x = Player.Instance.transform.position.x;
        slider.SetValueWithoutNotify(
            Mathf.InverseLerp(stageStartX, stageEndX, x));
    }
    
    private void SetCurrentTime(int value)
    {
        currentTime = Mathf.Max(0, value);
        if (currentTimeText != null)
            currentTimeText.text = currentTime.ToString();
    }

    private void OnHeartbeat(int elapsedTime)
    {
        SetCurrentTime(timeLimit - elapsedTime);
    }

    private void OnDestroy()
    {
        if (_clock != null)
        {
            _clock.Heartbeat -= OnHeartbeat;
        }
        
        if (Instance == this)
            Instance = null;
    }
}
