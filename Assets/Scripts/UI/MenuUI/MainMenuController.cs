using UnityEngine;
using UnityEngine.UI;


public class MainMenuController : MonoBehaviour
{
    [Header("씬")]
    [SerializeField] private string firstGameSceneName = "GameScene";

    [Header("버튼")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    private PauseMenuController pauseMenuController;

    private void Awake()
    {
        newGameButton.onClick.AddListener(StartNewGame);
        continueButton.onClick.AddListener(ContinueGame);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(QuitGame);
    }

    private void Start()
    {
        // TODO: 저장 데이터가 있을 때만 활성화
        continueButton.interactable = false;

        pauseMenuController = FindAnyObjectByType<PauseMenuController>();
    }

    private void StartNewGame()
    {
        if (SceneLoadManager.Instance.IsLoading) return;

        SetButtonsInteractable(false); // 페이드 중 중복 클릭 방지
        SceneLoadManager.Instance.LoadScene(firstGameSceneName);
    }

    private void ContinueGame()
    {
        // TODO: 저장 시스템 구현 후 저장된 씬으로 이동
    }

    private void OpenSettings()
    {
        pauseMenuController.OpenSettings();
    }

    private void QuitGame()
    {
        GameManager.Instance.QuitGame();
    }

    private void SetButtonsInteractable(bool interactable)
    {
        newGameButton.interactable = interactable;
        continueButton.interactable = false; // 저장 시스템 전까지 항상 비활성
        settingsButton.interactable = interactable;
        quitButton.interactable = interactable;
    }
}
