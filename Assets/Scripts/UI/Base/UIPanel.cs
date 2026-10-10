using System;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private CanvasGroup Group
    {
        get
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            return canvasGroup;
        }
    }

    // 닫기 연출 중에는 activeSelf가 아직 true이므로 별도 플래그로 판정
    public bool IsOpen { get; private set; }

    // 패널 안의 "닫기" 버튼 → RequestClose 연결. 실제로 닫는 것은 패널을 연 쪽에서 처리
    public event Action CloseRequested;
    public void RequestClose() => CloseRequested?.Invoke();

    public virtual void Show()
    {
        IsOpen = true;
        if (!gameObject.activeSelf) Group.alpha = 0f;
        gameObject.SetActive(true);

        Group.DOKill();
        Group.interactable = true;
        Group.blocksRaycasts = true;
        Group.DOFade(1f, fadeDuration).SetUpdate(true);
    }

    public virtual void Hide()
    {
        IsOpen = false;
        Group.DOKill();
        Group.interactable = false;
        Group.blocksRaycasts = false;
        Group.DOFade(0f, fadeDuration).SetUpdate(true)
             .OnComplete(() => gameObject.SetActive(false));
    }

    public void HideImmediate()
    {
        IsOpen = false;
        Group.DOKill();
        Group.alpha = 0f;
        Group.interactable = false;
        Group.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}