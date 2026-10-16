using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button continueButton;
    [FormerlySerializedAs("newGameButton")]          // 기존 인스펙터 연결 유지
    [SerializeField] private Button startButton;     // 슬롯 선택
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    // Systems 프리팹(DontDestroyOnLoad)에 있으므로 인스펙터로 연결할 수 없어 실행 시 검색
    private PauseMenuController pauseMenu;

    private void Awake()
    {
        pauseMenu = FindAnyObjectByType<PauseMenuController>();
        if (pauseMenu == null)
            Debug.LogError("[MainMenu] PauseMenuController를 찾을 수 없습니다. Systems 프리팹을 확인하세요.");

        continueButton.onClick.AddListener(ContinueGame);
        startButton.onClick.AddListener(() => pauseMenu.OpenProfileSelect());
        settingsButton.onClick.AddListener(() => pauseMenu.OpenSettings());
        quitButton.onClick.AddListener(() => pauseMenu.ConfirmQuitFromTitle());
    }

    private void OnEnable() => SaveManager.Instance.SlotsChanged += RefreshContinueButton;
    private void OnDisable()
    {
        if (SaveManager.HasInstance)
            SaveManager.Instance.SlotsChanged -= RefreshContinueButton;
    }

    private void Start()
    {
        RefreshContinueButton();
    }

    /// <summary>가장 최근에 플레이한 프로필 이어하기</summary>
    private void ContinueGame()
    {
        if (!SaveManager.Instance.TryGetLatestSlot(out int slot)) return;

        if (SaveManager.Instance.Load(slot))
            SetButtonsInteractable(false);
    }

    // 슬롯 선택 화면에서 프로필을 지우면 이어하기 버튼 상태 갱신
    private void RefreshContinueButton()
    {
        continueButton.interactable = SaveManager.Instance.TryGetLatestSlot(out _);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        continueButton.interactable = interactable && SaveManager.Instance.TryGetLatestSlot(out _);
        startButton.interactable = interactable;
        settingsButton.interactable = interactable;
        quitButton.interactable = interactable;
    }
}