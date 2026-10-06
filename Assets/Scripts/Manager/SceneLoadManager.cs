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

    public string TargetSceneName { get; private set; }
    private bool isLoading = false;

    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }


    public void LoadScene(string sceneName)
    {
        if (isLoading) return;
        StartCoroutine(TransitionToLoadingScene(sceneName));
    }

    private IEnumerator TransitionToLoadingScene(string sceneName)
    {
        isLoading = true;
        TargetSceneName = sceneName;

        // 페이드 아웃
        fadeCanvasGroup.blocksRaycasts = true;

        yield return fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).WaitForCompletion();

        // 로딩 씬으로 이동
        SceneManager.LoadScene("LoadingScene");

        // 로딩 씬 진입 직후 화면
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).WaitForCompletion();
    }

    public IEnumerator FadeOut()
    {
        fadeCanvasGroup.blocksRaycasts = true;
        yield return fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).WaitForCompletion();
    }

    public IEnumerator FadeIn()
    {
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).WaitForCompletion();
        fadeCanvasGroup.blocksRaycasts = false;
        isLoading = false;
    }
}
