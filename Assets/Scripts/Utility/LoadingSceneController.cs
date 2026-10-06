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

    private void Start()
    {
        StartCoroutine(LoadTargetSceneProcess());
    }

    private IEnumerator LoadTargetSceneProcess()
    {

        string targetScene = SceneLoadManager.Instance.TargetSceneName;

        if (progressBar != null) progressBar.fillAmount = 0f;
        if (progressText != null) progressText.text = "0%";

        // 비동기 로딩 시작 및 자동 전환 막기
        AsyncOperation op = SceneManager.LoadSceneAsync(targetScene);
        op.allowSceneActivation = false;

        float timer = 0f;

        // 진행도 UI 업데이트
        while (!op.isDone)
        {
            yield return null;
            timer += Time.deltaTime;

            if (op.progress < 0.9f)
            {
                progressBar.fillAmount = Mathf.Lerp(progressBar.fillAmount, op.progress, timer);
                if (progressBar.fillAmount >= op.progress) timer = 0f;
            }
            else
            {
                progressBar.fillAmount = Mathf.Lerp(progressBar.fillAmount, 1f, timer);

                if (progressBar.fillAmount >= 1.0f)
                {
                    break;
                }
            }

            if (progressText != null)
                progressText.text = $"{Mathf.RoundToInt(progressBar.fillAmount * 100)}%";
        }

        // 로딩 UI 연출 완료
        yield return StartCoroutine(SceneLoadManager.Instance.FadeOut());

        // 실제 씬 전환
        op.allowSceneActivation = true;

        yield return new WaitUntil(() => op.isDone);

        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(SceneLoadManager.Instance.FadeIn());
    }
}
