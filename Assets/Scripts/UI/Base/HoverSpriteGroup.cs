using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자식 버튼(PointerHover) 중 마우스가 올라간 버튼에 "등불이 켜지는" 연출을 적용합니다.
/// 평소 이미지 위에 호버 이미지를 겹쳐 두고, 투명도를 깜빡이며 올렸다가 천천히 내립니다.
/// </summary>
public class HoverSpriteGroup : MonoBehaviour
{
    [Header("이미지")]
    [Tooltip("평소 이미지. 비워 두면 각 버튼에 원래 들어 있던 이미지 사용")]
    [SerializeField] private Sprite normalSprite;
    [Tooltip("불이 켜진 이미지 (평소 이미지 위에 겹쳐 그림)")]
    [SerializeField] private Sprite hoverSprite;

    [Header("불 켜짐")]
    [Tooltip("불이 붙을 때 깜빡이는 한 번의 시간")]
    [SerializeField, Min(0f)] private float flickerStep = 0.06f;
    [Tooltip("깜빡임 후 완전히 밝아지는 시간")]
    [SerializeField, Min(0f)] private float lightUpDuration = 0.15f;

    [Header("불 꺼짐")]
    [SerializeField, Min(0f)] private float lightOffDuration = 0.3f;

    [Header("호버 중 일렁임")]
    [SerializeField] private bool idleFlicker = true;
    [Tooltip("일렁일 때 어두워지는 정도 (0 = 없음)")]
    [SerializeField, Range(0f, 0.5f)] private float idleFlickerAmount = 0.12f;
    [SerializeField, Min(0.1f)] private float idleFlickerDuration = 0.9f;

    private class Entry
    {
        public Image glow;
        public Tween tween;
    }

    private readonly Dictionary<PointerHover, Entry> entries = new Dictionary<PointerHover, Entry>();
    private PointerHover current;

    private void Awake()
    {
        // 꺼져 있는 버튼까지 포함해서 자식의 PointerHover를 모두 찾아 등록
        foreach (PointerHover hover in GetComponentsInChildren<PointerHover>(true))
        {
            if (!hover.TryGetComponent(out Image image)) continue;

            if (normalSprite != null)
                image.sprite = normalSprite;

            entries[hover] = new Entry { glow = CreateGlow(hover.transform, image) };
            hover.HoverChanged += OnHoverChanged;
        }
    }

    private void OnDestroy()
    {
        foreach (var pair in entries)
        {
            if (pair.Key != null)
                pair.Key.HoverChanged -= OnHoverChanged;
            pair.Value.tween?.Kill();
        }
    }

    /// <summary>버튼 Image와 같은 크기의 "불 켜진" 이미지를 버튼 글자 아래에 겹쳐 생성</summary>
    private Image CreateGlow(Transform button, Image source)
    {
        var go = new GameObject("HoverGlow", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(button, false);
        rect.SetAsFirstSibling(); // 버튼 글자(뒤쪽 자식)보다 아래에 그려지도록
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var glow = go.GetComponent<Image>();
        glow.sprite = hoverSprite;
        glow.type = source.type;
        glow.preserveAspect = source.preserveAspect;
        glow.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        glow.raycastTarget = false; // 클릭은 버튼 본체가 받음

        Color c = glow.color;
        c.a = 0f;
        glow.color = c;

        return glow;
    }

    private void OnHoverChanged(PointerHover hover, bool isHovering)
    {
        if (isHovering)
        {
            // 이전에 호버된 버튼이 남아 있으면 먼저 끄기 (항상 하나만 켜짐)
            if (current != null && current != hover)
                LightOff(current);

            current = hover;
            LightUp(hover);
        }
        else
        {
            LightOff(hover);
            if (current == hover) current = null;
        }
    }

    private void LightUp(PointerHover hover)
    {
        if (hoverSprite == null || !entries.TryGetValue(hover, out Entry entry)) return;

        entry.tween?.Kill();

        // 불이 붙는 느낌: 확 밝아졌다 → 잠깐 죽었다 → 완전히 밝아짐
        entry.tween = DOTween.Sequence()
            .Append(entry.glow.DOFade(0.7f, flickerStep))
            .Append(entry.glow.DOFade(0.3f, flickerStep))
            .Append(entry.glow.DOFade(1f, lightUpDuration).SetEase(Ease.OutQuad))
            .SetUpdate(true)
            .SetLink(entry.glow.gameObject)
            .OnComplete(() => StartIdleFlicker(entry));
    }

    private void StartIdleFlicker(Entry entry)
    {
        if (!idleFlicker || idleFlickerAmount <= 0f) return;

        // 등불이 은은하게 일렁이는 느낌
        entry.tween = entry.glow.DOFade(1f - idleFlickerAmount, idleFlickerDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(entry.glow.gameObject);
    }

    private void LightOff(PointerHover hover)
    {
        if (!entries.TryGetValue(hover, out Entry entry)) return;

        entry.tween?.Kill();

        // 불이 사그라지는 느낌: 처음엔 천천히, 끝에서 빠르게
        entry.tween = entry.glow.DOFade(0f, lightOffDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true)
            .SetLink(entry.glow.gameObject);
    }
}