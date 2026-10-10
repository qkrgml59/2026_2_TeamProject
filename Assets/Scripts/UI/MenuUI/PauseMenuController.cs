using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private UIPanel pauseMenuPanel;
    [SerializeField] private UIPanel saveLoadPanel;
    [SerializeField] private UIPanel settingsPanel;

    [Header("버튼")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveLoadButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    private readonly Stack<UIPanel> panelStack = new Stack<UIPanel>();

    public bool IsOpen => panelStack.Count > 0;

    private void Awake()
    {
        resumeButton.onClick.AddListener(Resume);
        saveLoadButton.onClick.AddListener(() => Push(saveLoadPanel));
        settingsButton.onClick.AddListener(() => Push(settingsPanel));
        mainMenuButton.onClick.AddListener(GoToMainMenu);
        quitButton.onClick.AddListener(QuitGame);

        saveLoadPanel.CloseRequested += Back;
        settingsPanel.CloseRequested += Back;
    }

    private void OnDestroy()
    {
        saveLoadPanel.CloseRequested -= Back;
        settingsPanel.CloseRequested -= Back;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Start()
    {
        CloseAllImmediate();
    }

    // Systems는 씬이 바뀌어도 유지되므로, 씬 로드 시 열려 있던 패널을 강제로 닫음
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            CloseAllImmediate();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        // 씬 전환(페이드) 중에는 ESC 무시
        if (SceneLoadManager.Instance != null && SceneLoadManager.Instance.IsLoading) return;

        if (IsOpen)
            Back();
        else if (GameManager.Instance.CurrentState == GameState.Playing)
            Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        GameManager.Instance.ChangeState(GameState.Paused);
        Push(pauseMenuPanel);
    }

    /// <summary>1. 돌아가기: 열린 패널을 모두 닫고 게임 재개</summary>
    public void Resume()
    {
        while (panelStack.Count > 0)
            panelStack.Pop().Hide();

        GameManager.Instance.RestorePreviousState();
    }

    /// <summary>한 단계 뒤로. 마지막 패널을 닫으면 게임 재개</summary>
    public void Back()
    {
        if (panelStack.Count == 0) return;

        panelStack.Pop().Hide();

        if (panelStack.Count > 0)
            panelStack.Peek().Show();
        else
            GameManager.Instance.RestorePreviousState();
    }

    private void Push(UIPanel panel)
    {
        if (panelStack.Count > 0)
            panelStack.Peek().Hide();

        panelStack.Push(panel);
        panel.Show();
    }

    private void CloseAllImmediate()
    {
        panelStack.Clear();
        pauseMenuPanel.HideImmediate();
        saveLoadPanel.HideImmediate();
        settingsPanel.HideImmediate();
    }

    /// <summary>4. 메뉴로 나가기</summary>
    private void GoToMainMenu()
    {
        // TODO: "저장하지 않은 진행 상황은 사라집니다" 확인 팝업
        CloseAllImmediate(); // 메뉴만 닫고 Paused 상태는 유지 → 멈춘 화면 위로 페이드아웃
        GameManager.Instance.LoadTitleScene();
    }

    public void OpenSettings()
    {
        if (IsOpen) return;
        Push(settingsPanel);
    }

    /// <summary>5. 게임 종료</summary>
    private void QuitGame()
    {
        GameManager.Instance.QuitGame();
    }
}