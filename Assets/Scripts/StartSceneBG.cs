using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartSceneBG : MonoBehaviour
{
    [Header("Background Buildings")]
    [SerializeField] private RectTransform firstBuildingGroup;
    [SerializeField] private RectTransform secondBuildingGroup;
    [SerializeField, Min(0f)] private float scrollSpeed = 10f;
    [SerializeField, Min(0.01f)] private float imageOffset = 1920f;
    [Header("Menu Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private string gameplaySceneName = "SampleScene";

    private float _loopDistance;
    private float _recycleX;

    private void Awake()
    {
        // slot은 배경 건물 레이어만 묶은 오브젝트다. 버튼과 타이틀은 별도 부모라 움직이지 않는다.
        if (firstBuildingGroup == null)
        {
            firstBuildingGroup = GameObject.Find("slot")?.GetComponent<RectTransform>();
        }

        if (secondBuildingGroup == null)
        {
            secondBuildingGroup = GameObject.Find("slot (1)")?.GetComponent<RectTransform>();
        }

        if (startButton == null)
        {
            startButton = GameObject.Find("StartButton")?.GetComponent<Button>();
        }

        if (quitButton == null)
        {
            quitButton = GameObject.Find("QuitButton")?.GetComponent<Button>();
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }

        if (firstBuildingGroup == null || secondBuildingGroup == null)
        {
            return;
        }

        _loopDistance = Mathf.Abs(
            secondBuildingGroup.anchoredPosition.x - firstBuildingGroup.anchoredPosition.x);
        if (_loopDistance < 0.01f)
        {
            _loopDistance = imageOffset;
        }

        _recycleX = Mathf.Min(firstBuildingGroup.anchoredPosition.x, secondBuildingGroup.anchoredPosition.x)
                    - _loopDistance;
    }

    private void Update()
    {
        if (firstBuildingGroup == null || secondBuildingGroup == null)
        {
            return;
        }

        MoveLeft(firstBuildingGroup);
        MoveLeft(secondBuildingGroup);

        RecycleIfNeeded(firstBuildingGroup, secondBuildingGroup);
        RecycleIfNeeded(secondBuildingGroup, firstBuildingGroup);
    }

    private void MoveLeft(RectTransform background)
    {
        Vector2 position = background.anchoredPosition;
        position.x -= scrollSpeed * Time.deltaTime;
        background.anchoredPosition = position;
    }

    private void RecycleIfNeeded(RectTransform background, RectTransform otherBackground)
    {
        if (background.anchoredPosition.x <= _recycleX)
        {
            Vector2 position = background.anchoredPosition;
            position.x = otherBackground.anchoredPosition.x + _loopDistance;
            background.anchoredPosition = position;
        }
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }
    }
}
