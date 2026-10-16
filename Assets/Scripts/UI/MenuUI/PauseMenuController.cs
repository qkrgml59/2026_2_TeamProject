using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private UIPanel pauseMenuPanel;
    [SerializeField] private ProfileSelectView profilePanel;
    [SerializeField] private UIPanel settingsPanel;
    [SerializeField] private ConfirmPopup confirmPopup;

    [Header("버튼")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [FormerlySerializedAs("mainMenuButton")]        // 기존 인스펙터 연결 유지
    [SerializeField] private Button saveAndExitButton;
    [SerializeField] private Button quitButton;

    [Header("문구")]
    [SerializeField, TextArea] private string quitMessageInGame = "정말 종료하시겠습니까?\n저장하지 않은 데이터는 삭제됩니다.";
    [SerializeField, TextArea] private string quitMessageTitle = "정말 종료하시겠습니까?";

    private readonly Stack<UIPanel> panelStack = new Stack<UIPanel>();

    public bool IsOpen => panelStack.Count > 0;

    private void Awake()
    {
        resumeButton.onClick.AddListener(Resume);
        settingsButton.onClick.AddListener(() => Push(settingsPanel));
        saveAndExitButton.onClick.AddListener(SaveAndExitToTitle);
        quitButton.onClick.AddListener(() => ShowConfirm(quitMessageInGame, () => GameManager.Instance.QuitGame()));

        profilePanel.CloseRequested += Back;
        profilePanel.ProfileSelected += HandleProfileSelected;
        settingsPanel.CloseRequested += Back;
        confirmPopup.CloseRequested += Back;
    }

    private void OnDestroy()
    {
        profilePanel.CloseRequested -= Back;
        profilePanel.ProfileSelected -= HandleProfileSelected;
        settingsPanel.CloseRequested -= Back;
        confirmPopup.CloseRequested -= Back;
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
        if (SceneLoadManager.Instance != null && SceneLoadManager.Instance.IsLoading) return;

        if (IsOpen)
            Back();   // 팝업이 열려 있으면 ESC = 취소
        else if (GameManager.Instance.CurrentState == GameState.Playing)
            Open();
    }

    // ───────── 인게임 ─────────

    public void Open()
    {
        if (IsOpen) return;
        GameManager.Instance.ChangeState(GameState.Paused);
        Push(pauseMenuPanel);
    }

    /// <summary>1. 돌아가기</summary>
    public void Resume()
    {
        while (panelStack.Count > 0)
            panelStack.Pop().Hide();

        GameManager.Instance.RestorePreviousState();
    }

    /// <summary>3. 저장 후 나가기: 현재 위치 저장 → 타이틀</summary>
    private void SaveAndExitToTitle()
    {
        SaveManager.Instance.AutoSave();
        CloseAllImmediate();
        GameManager.Instance.LoadTitleScene();
    }

    // ───────── 타이틀 화면용 (일시정지 없음) ─────────

    public void OpenSettings()
    {
        if (IsOpen) return;
        Push(settingsPanel);
    }

    public void OpenProfileSelect()
    {
        if (IsOpen) return;
        Push(profilePanel);
    }

    public void ConfirmQuitFromTitle()
    {
        if (IsOpen) return;
        ShowConfirm(quitMessageTitle, () => GameManager.Instance.QuitGame());
    }

    private void HandleProfileSelected(int slot)
    {
        if (SaveManager.Instance.StartOrContinue(slot))
            CloseAllImmediate();
    }

    // ───────── 공통 ─────────

    /// <summary>확인 팝업 표시. 뒤의 패널은 보이는 채로 입력만 막음</summary>
    public void ShowConfirm(string message, Action onConfirm)
    {
        confirmPopup.Setup(message, onConfirm);
        Push(confirmPopup, keepPreviousVisible: true);
    }

    public void Back()
    {
        if (panelStack.Count == 0) return;

        panelStack.Pop().Hide();

        if (panelStack.Count > 0)
            panelStack.Peek().Show();   // 숨겼던 패널은 다시 표시, 입력만 막았던 패널은 입력 복구
        else
            GameManager.Instance.RestorePreviousState(); // 타이틀에서는 Paused가 아니므로 아무 일도 없음
    }

    private void Push(UIPanel panel, bool keepPreviousVisible = false)
    {
        if (panelStack.Count > 0)
        {
            if (keepPreviousVisible)
                panelStack.Peek().SetInteractable(false);
            else
                panelStack.Peek().Hide();
        }

        panelStack.Push(panel);
        panel.Show();
    }

    private void CloseAllImmediate()
    {
        panelStack.Clear();
        pauseMenuPanel.HideImmediate();
        profilePanel.HideImmediate();
        settingsPanel.HideImmediate();
        confirmPopup.HideImmediate();
    }
}