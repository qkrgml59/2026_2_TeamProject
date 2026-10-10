using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SceneLoadManager : SingletonMonoBehaviour<SceneLoadManager>
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private string loadingSceneName = "LoadingScene";

    public string TargetSceneName { get; private set; }

    // 외부(일시정지 메뉴 등)에서 전환 중인지 확인하는 용도
    public bool IsLoading { get; private set; }

    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();

        if (fadeCanvasGroup == null)
        {
            Debug.LogError("[SceneLoadManager] fadeCanvasGroup이 연결되지 않았습니다.");
            return;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    public void LoadScene(string sceneName)
    {
        if (IsLoading || fadeCanvasGroup == null) return;
        StartCoroutine(TransitionToLoadingScene(sceneName));
    }

    private IEnumerator TransitionToLoadingScene(string sceneName)
    {
        IsLoading = true;
        TargetSceneName = sceneName;

        yield return FadeOut();

        SceneManager.LoadScene(loadingSceneName);
        yield return null; // 씬 로드는 다음 프레임에 완료됨. 로드 직후의 프레임 지연으로 페이드가 건너뛰지 않도록 대기

        fadeCanvasGroup.DOKill();
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).WaitForCompletion();
        // blocksRaycasts와 IsLoading은 로딩 씬이 대상 씬 진입 후 FadeIn()을 호출할 때 해제됨
    }

    public IEnumerator FadeOut()
    {
        fadeCanvasGroup.DOKill();
        fadeCanvasGroup.blocksRaycasts = true;
        yield return fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).WaitForCompletion();
    }

    public IEnumerator FadeIn()
    {
        fadeCanvasGroup.DOKill();
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).WaitForCompletion();
        fadeCanvasGroup.blocksRaycasts = false;
        IsLoading = false;
    }

    /// <summary>
    /// 로딩 씬에서 호출. 씬 활성화 → 페이드인까지 이 오브젝트(씬 전환에도 유지됨)에서 실행하므로
    /// 로딩 씬이 언로드되어도 중단되지 않습니다.
    /// </summary>
    public void CompleteLoading(AsyncOperation op)
    {
        StartCoroutine(CompleteLoadingRoutine(op));
    }

    /// <summary>로딩 실패 시 페이드와 입력 차단을 해제합니다.</summary>
    public void AbortLoading()
    {
        StartCoroutine(FadeIn());
    }

    private IEnumerator CompleteLoadingRoutine(AsyncOperation op)
    {
        yield return FadeOut();

        op.allowSceneActivation = true;
        yield return new WaitUntil(() => op.isDone);

        yield return new WaitForSecondsRealtime(0.1f);
        yield return FadeIn();
    }
}