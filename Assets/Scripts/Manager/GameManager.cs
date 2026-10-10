using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState 
{ 
    None,       
    Title,      // 메뉴
    Playing,    // 플레이 중
    Paused,     // 일시 정지
    Cutscene,   // 연출 중
    GameOver,   // 게임종료
    Loading     //로딩 중
}

[DefaultExecutionOrder(-100)]
public class GameManager : SingletonMonoBehaviour<GameManager>
{
    [Header("씬 설정")]
    [SerializeField] private string titleSceneName = "Mainmenu";
    [SerializeField] private string loadingSceneName = "LoadingScene";

    [Header("시간 설정")]
    [SerializeField, Range(0.01f, 1f)] private float gameOverTimeScale = 0.2f;

    public GameState CurrentState { get; private set; } = GameState.None;

    private GameState previousState = GameState.None;
    private float defaultFixedDeltaTime;

    public event Action<GameState> OnGameStateChanged;

    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();

        defaultFixedDeltaTime = Time.fixedDeltaTime;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 이미 로드된 씬이 있을 때만 즉시 초기화 (Bootstrapper 경유 시에는 sceneLoaded에서 처리)
        Scene active = SceneManager.GetActiveScene();
        if (active.isLoaded)
            InitializeStateForScene(active.name);
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        InitializeStateForScene(scene.name);
    }

    private void InitializeStateForScene(string sceneName)
    {
        previousState = GameState.None;

        if (sceneName == titleSceneName)
            ChangeState(GameState.Title);
        else if (sceneName == loadingSceneName)
            ChangeState(GameState.Loading);
        else
            ChangeState(GameState.Playing);
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        if (newState == GameState.Paused)
            previousState = CurrentState;

        CurrentState = newState;
        HandleTimeScale(newState);

        Debug.Log($"[GameManager] State Changed to: {newState}");
        OnGameStateChanged?.Invoke(newState);
    }

    public void RestorePreviousState()
    {
        if (CurrentState != GameState.Paused) return;
        ChangeState(previousState != GameState.None ? previousState : GameState.Playing);
    }

    /// <summary>페이드 + 로딩 씬을 거쳐 타이틀로 이동</summary>
    public void LoadTitleScene()
    {
        SceneLoadManager.Instance.LoadScene(titleSceneName);
    }

    private void HandleTimeScale(GameState state)
    {
        switch (state)
        {
            case GameState.Paused:
                ApplyTime(0f, pauseAudio: true);
                break;

            case GameState.GameOver:
                ApplyTime(gameOverTimeScale, pauseAudio: false);
                break;

            default: // Title, Playing, Cutscene, Loading
                ApplyTime(1f, pauseAudio: false);
                break;
        }
    }

    private void ApplyTime(float timeScale, bool pauseAudio)
    {
        Time.timeScale = timeScale;

        if (timeScale > 0f)
            Time.fixedDeltaTime = defaultFixedDeltaTime * timeScale;

        AudioListener.pause = pauseAudio;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Application.Quit이 동작하지 않음
#else
    Application.Quit();
#endif
    }

}