using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingSceneController : MonoBehaviour
{
    [Header("Progress")]
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField, Min(0.1f)] private float fillSpeed = 1f; // 초당 채워지는 비율 (1 = 최소 1초)

    private void Start()
    {
        StartCoroutine(LoadTargetSceneProcess());
    }

    private IEnumerator LoadTargetSceneProcess()
    {
        string targetScene = SceneLoadManager.Instance.TargetSceneName;

        // 씬 이름 오타나 Build Profiles 미등록 확인
        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError($"[LoadingScene] '{targetScene}' 씬을 로드할 수 없습니다. 씬 이름과 Build Profiles 등록을 확인하세요.");
            SceneLoadManager.Instance.AbortLoading();
            yield break;
        }

        UpdateProgressUI(0f);

        // 비동기 로딩 시작, 자동 전환은 막음
        AsyncOperation op = SceneManager.LoadSceneAsync(targetScene);
        op.allowSceneActivation = false;

        // allowSceneActivation = false 상태에서는 progress가 0.9에서 멈추므로 0~0.9를 0~1로 환산
        float displayed = 0f;
        while (displayed < 1f)
        {
            float target = Mathf.Clamp01(op.progress / 0.9f);
            displayed = Mathf.MoveTowards(displayed, target, fillSpeed * Time.unscaledDeltaTime);
            UpdateProgressUI(displayed);
            yield return null;
        }

        // 이후 과정(페이드아웃 → 씬 활성화 → 페이드인)은 SceneLoadManager가 처리
        SceneLoadManager.Instance.CompleteLoading(op);
    }

    private void UpdateProgressUI(float value)
    {
        if (progressBar != null) progressBar.fillAmount = value;
        if (progressText != null) progressText.text = $"{Mathf.RoundToInt(value * 100)}%";
    }
}