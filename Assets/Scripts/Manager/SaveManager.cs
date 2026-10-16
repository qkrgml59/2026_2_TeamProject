using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 프로필 3개 + 자동 저장.
/// - 진행도(Progress)가 바뀌거나 맵에 들어가면 자동 저장 (공중이면 착지할 때까지 대기)
/// - 이어하기: 마지막 저장 위치 / 사망: 현재 맵 입구에서 부활
/// </summary>
public class SaveManager : SingletonMonoBehaviour<SaveManager>
{
    public const int SlotCount = 3;

    [Header("새 게임")]
    [SerializeField] private string newGameSceneName = "GameScene";

    [Header("자동 저장")]
    [Tooltip("일정 시간마다 자동 저장 (초). 0이면 사용 안 함")]
    [SerializeField, Min(0f)] private float periodicSaveInterval = 30f;

    public GameProgress Progress { get; } = new GameProgress();
    public float PlayTime { get; private set; }

    /// <summary> 현재 플레이 중인 프로필 (-1 = 없음) </summary>
    public int CurrentSlot { get; private set; } = -1;

    /// <summary> 슬롯 파일이 생성·갱신·삭제됨 (타이틀 버튼 갱신용) </summary>
    public event Action SlotsChanged;

    private SaveData pendingLoad;

    // 저장 주기 타이머
    private float periodicSaveTimer;

    // 현재 맵에 들어온 순간의 상태 → 사망 시 부활 지점 (파일에는 저장하지 않음)
    private string entrySceneName;
    private List<SaveEntry> entrySessionEntries;

    private bool autoSaveRequested;
    private bool entryCapturePending;   // 착지하면 부활 지점을 다시 기록

    // 저장 가능 조건 (플레이어 착지 여부 등)
    private readonly List<ISaveCondition> saveConditions = new List<ISaveCondition>();

    private string saveDirectory;
    private string SaveDirectory
    {
        get
        {
            if (saveDirectory == null)
            {
                saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
                Directory.CreateDirectory(saveDirectory);
            }
            return saveDirectory;
        }
    }

    private bool CanSave
    {
        get
        {
            if (!IsValidSlot(CurrentSlot)) return false;
            if (SceneLoadManager.Instance != null && SceneLoadManager.Instance.IsLoading) return false;

            GameState state = GameManager.Instance.CurrentState;
            return state == GameState.Playing || state == GameState.Paused;
        }
    }

    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Progress.OnChanged += OnProgressChanged;
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Progress.OnChanged -= OnProgressChanged;
        base.OnDestroy();
    }

    private void Update()
    {
        GameState state = GameManager.Instance.CurrentState;
        if (state == GameState.Playing || state == GameState.Cutscene)
            PlayTime += Time.unscaledDeltaTime;

        if (periodicSaveInterval > 0f && state == GameState.Playing)
        {
            periodicSaveTimer += Time.unscaledDeltaTime;
            if (periodicSaveTimer >= periodicSaveInterval)
            {
                periodicSaveTimer = 0f;
                RequestAutoSave();
            }
        }
    }

    private void LateUpdate()
    {
        if (!autoSaveRequested && !entryCapturePending) return;

        // 공중에 있으면 착지할 때까지 대기
        if (!IsSafeToSave()) return;

        if (entryCapturePending && GameManager.Instance.CurrentState == GameState.Playing)
        {
            entryCapturePending = false;
            entrySceneName = SceneManager.GetActiveScene().name;
            entrySessionEntries = CaptureSessionEntries();
        }

        if (autoSaveRequested && CanSave)
        {
            autoSaveRequested = false;
            AutoSave();
        }
    }

    private void OnProgressChanged(string key) => RequestAutoSave();

    /// <summary> 안전한 시점(착지 후)에 자동 저장 </summary>
    public void RequestAutoSave() => autoSaveRequested = true;

    // ───────── 저장 조건 ─────────

    public void RegisterCondition(ISaveCondition condition)
    {
        if (!saveConditions.Contains(condition))
            saveConditions.Add(condition);
    }

    public void UnregisterCondition(ISaveCondition condition)
    {
        saveConditions.Remove(condition);
    }

    private bool IsSafeToSave()
    {
        foreach (ISaveCondition condition in saveConditions)
        {
            if (!condition.IsSafeToSave) return false;
        }
        return true;
    }

    // ───────── 시작 ─────────

    /// <summary> 슬롯 선택: 빈 슬롯이면 새 게임, 데이터가 있으면 이어하기 </summary>
    public bool StartOrContinue(int slot) => HasSave(slot) ? Load(slot) : StartNewGame(slot);

    public bool StartNewGame(int slot)
    {
        if (!IsValidSlot(slot) || HasSave(slot) || SceneLoadManager.Instance.IsLoading) return false;

        if (!Application.CanStreamedLevelBeLoaded(newGameSceneName))
        {
            Debug.LogError($"[SaveManager] 새 게임 씬 '{newGameSceneName}'을 로드할 수 없습니다.");
            return false;
        }

        CurrentSlot = slot;
        ResetSession();
        PlayTime = 0f;
        Progress.Clear();

        SceneLoadManager.Instance.LoadScene(newGameSceneName);
        return true;
    }

    public bool Load(int slot)
    {
        if (SceneLoadManager.Instance.IsLoading) return false;

        SaveData data = PeekSlot(slot);
        if (data == null) return false;

        if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
        {
            Debug.LogError($"[SaveManager] 세이브의 씬 '{data.sceneName}'을 로드할 수 없습니다.");
            return false;
        }

        CurrentSlot = slot;
        ResetSession();
        pendingLoad = data;
        PlayTime = data.playTime;

        SceneLoadManager.Instance.LoadScene(data.sceneName);
        return true;
    }

    /// <summary> 사망 시 호출: 현재 맵 입구에서 부활 (진행도 유지) </summary>
    public bool RespawnAtMapEntrance()
    {
        if (SceneLoadManager.Instance.IsLoading || entrySceneName == null) return false;

        pendingLoad = new SaveData
        {
            sceneName = entrySceneName,
            playTime = PlayTime,
            progress = Progress.ToData(),
            sessionEntries = entrySessionEntries,
        };

        SceneLoadManager.Instance.LoadScene(entrySceneName);
        return true;
    }

    // 이전 플레이에서 남은 요청이 새 프로필에 저장되지 않도록 초기화
    private void ResetSession()
    {
        pendingLoad = null;
        entrySceneName = null;
        entrySessionEntries = null;
        autoSaveRequested = false;
        entryCapturePending = false;
    }

    // ───────── 저장 ─────────

    /// <summary> 즉시 저장 (저장 후 나가기 등). 위치는 PlayerSaveable이 마지막 착지 위치를 넘겨줌 </summary>
    public bool AutoSave()
    {
        if (!CanSave) return false;

        SaveData data = new SaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
            savedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
            playTime = PlayTime,
            progress = Progress.ToData(),
            sessionEntries = CaptureSessionEntries(),
        };

        try
        {
            string path = GetPath(CurrentSlot);
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));

            if (File.Exists(path)) File.Replace(tempPath, path, null);
            else File.Move(tempPath, path);

            SlotsChanged?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 슬롯 {CurrentSlot} 저장 실패: {e.Message}");
            return false;
        }
    }

    // ───────── 씬 전환 처리 ─────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        SaveData loaded = null;
        if (pendingLoad != null && pendingLoad.sceneName == scene.name)
        {
            loaded = pendingLoad;
            pendingLoad = null;

            // Start() 이전에 적용 → 퍼즐·보스가 Start()에서 클리어 여부를 확인할 수 있음
            Progress.FromData(loaded.progress);
        }

        StartCoroutine(AfterSceneLoaded(loaded));
    }

    private IEnumerator AfterSceneLoaded(SaveData loaded)
    {
        // Start()가 끝난 뒤 실행. 페이드인 전이라 화면에 보이지 않음
        yield return null;

        if (loaded != null)
            RestoreEntries(loaded.sessionEntries);

        // 타이틀·로딩 씬은 제외
        if (GameManager.Instance.CurrentState != GameState.Playing) yield break;

        // 부활 지점: 일단 진입 위치로 기록 (착지 전에 죽어도 이 맵에서 부활)
        entrySceneName = SceneManager.GetActiveScene().name;
        entrySessionEntries = CaptureSessionEntries();

        // 착지하면 부활 지점을 다시 기록하고 자동 저장
        entryCapturePending = true;
        RequestAutoSave();
    }

    // ───────── 조회 ─────────

    public bool HasSave(int slot) => IsValidSlot(slot) && File.Exists(GetPath(slot));

    public SaveData PeekSlot(int slot)
    {
        if (!HasSave(slot)) return null;

        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(GetPath(slot)));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 슬롯 {slot} 읽기 실패 (파일 손상): {e.Message}");
            return null;
        }
    }

    /// <summary> 가장 최근에 플레이한 프로필 </summary>
    public bool TryGetLatestSlot(out int latestSlot)
    {
        latestSlot = -1;
        DateTime latest = DateTime.MinValue;

        for (int i = 0; i < SlotCount; i++)
        {
            SaveData data = PeekSlot(i);
            if (data != null && data.SavedAt > latest)
            {
                latest = data.SavedAt;
                latestSlot = i;
            }
        }
        return latestSlot >= 0;
    }

    public void Delete(int slot)
    {
        if (!HasSave(slot)) return;

        File.Delete(GetPath(slot));
        if (CurrentSlot == slot) CurrentSlot = -1;
        SlotsChanged?.Invoke();
    }

    // ───────── 내부 도구 ─────────

    private bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

    private string GetPath(int slot) => Path.Combine(SaveDirectory, $"slot_{slot}.json");

    private static List<SaveEntry> CaptureSessionEntries()
    {
        List<SaveEntry> entries = new List<SaveEntry>();
        HashSet<string> usedIds = new HashSet<string>();

        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour is not ISaveable saveable) continue;

            if (string.IsNullOrEmpty(saveable.SaveId) || !usedIds.Add(saveable.SaveId))
            {
                Debug.LogWarning($"[SaveManager] SaveId가 비어 있거나 중복된 {saveable.GetType().Name}는 건너뜁니다.");
                continue;
            }
            entries.Add(new SaveEntry { id = saveable.SaveId, json = saveable.CaptureState() });
        }
        return entries;
    }

    private static void RestoreEntries(List<SaveEntry> entries)
    {
        if (entries == null) return;

        Dictionary<string, string> states = new Dictionary<string, string>();
        foreach (SaveEntry entry in entries)
            states[entry.id] = entry.json;

        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour is ISaveable saveable && states.TryGetValue(saveable.SaveId, out string json))
                saveable.RestoreState(json);
        }
    }
}