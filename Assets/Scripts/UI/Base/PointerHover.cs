using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[System.Serializable]
public class PointerUnityEvent : UnityEvent<PointerEventData> { }


public class PointerHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("호버 시")]
    [SerializeField, Min(0f)] private float hoverScaleMultiplier = 1.1f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.2f;

    [Header("클릭 시")]
    [SerializeField, Min(0f)] private float clickScaleMultiplier = 1.05f;
    [SerializeField, Min(0f)] private float clickDuration = 0.1f;

    [Header("공통")]
    [SerializeField] private Ease ease = Ease.OutQuad;
    [SerializeField] private bool ignoreTimeScale = true;
    [SerializeField] private bool leftClickOnly = true;

    [Header("클릭 이벤트")]
    [FormerlySerializedAs("onClickEvent")]
    [SerializeField] private UnityEvent onClick = new UnityEvent();
    [FormerlySerializedAs("onPointerClickEvent")]
    [SerializeField] private PointerUnityEvent onPointerClick = new PointerUnityEvent();

    public UnityEvent OnClick => onClick;
    public PointerUnityEvent OnPointerClickEvent => onPointerClick;

    private Selectable selectable;
    private Vector3 originalScale;
    private Tween scaleTween;

    private bool isHovering;
    private bool isPressed;

    private Vector3 HoverScale => originalScale * hoverScaleMultiplier;
    private Vector3 ClickScale => originalScale * clickScaleMultiplier;

    private bool IsInteractable => selectable == null || selectable.IsInteractable();

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        originalScale = transform.localScale;
    }

    private void OnDisable()
    {
        // 비활성화 시 상태와 크기를 원래대로 되돌림 (꺼졌다 켜졌을 때 커진 채로 남는 문제 방지)
        isHovering = false;
        isPressed = false;
        scaleTween?.Kill();
        transform.localScale = originalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        if (!IsInteractable) return;

        if (isPressed) ScaleTo(ClickScale, clickDuration);
        else ScaleTo(HoverScale, hoverDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        // 상호작용 불가 상태여도 원래 크기로는 항상 복귀
        ScaleTo(originalScale, hoverDuration);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsValidButton(eventData) || !IsInteractable) return;

        isPressed = true;
        ScaleTo(ClickScale, clickDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsValidButton(eventData) || !isPressed) return;

        // 상호작용 여부와 관계없이 눌림 상태는 항상 해제
        isPressed = false;

        bool showHover = isHovering && IsInteractable;
        ScaleTo(showHover ? HoverScale : originalScale, clickDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 같은 오브젝트에서 눌렀다 뗐을 때만 EventSystem이 호출해 줌
        if (!IsValidButton(eventData) || !IsInteractable) return;

        onClick.Invoke();
        onPointerClick.Invoke(eventData);
    }

    private bool IsValidButton(PointerEventData eventData) =>
        !leftClickOnly || eventData.button == PointerEventData.InputButton.Left;

    private void ScaleTo(Vector3 target, float duration)
    {
        scaleTween?.Kill();
        scaleTween = transform.DOScale(target, duration)
            .SetEase(ease)
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject);
    }
}
