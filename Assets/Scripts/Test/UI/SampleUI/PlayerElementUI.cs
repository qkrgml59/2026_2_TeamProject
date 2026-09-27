using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FourGuardians.CourseContent.Combat
{
    public sealed class PlayerElementUI : MonoBehaviour
    {
        [Header("참조 컴포넌트")]
        [SerializeField] private PlayerElementHandler playerElementHandler;

        [Header("UI 요소")]
        [SerializeField] private Text elementText;
        [SerializeField] private Image elementIcon;

        private ElementType lastElement = (ElementType)(-1);

        private void Update()
        {
            if (playerElementHandler == null) return;

            ElementType current = playerElementHandler.CurrentElement;

            //속성이 바뀔 때만 UI 업데이트
            if (current != lastElement)
            {
                UpdateUI(current);
                lastElement = current;
            }
        }

        private void UpdateUI(ElementType element)
        {
            Color color = GetColor(element);

            if (elementText != null)
            {
                elementText.text = GetName(element);
                elementText.color = color;
            }

            if (elementIcon != null)
            {
                elementIcon.color = color;
            }
        }

        private string GetName(ElementType type) => type switch
        {
            ElementType.Fire => "주작 (불)",
            ElementType.Water => "현무 (물)",
            ElementType.Electric => "청룡 (전기)",
            ElementType.Wind => "백호 (바람)",
            _ => "무속성"
        };

        private Color GetColor(ElementType type) => type switch
        {
            ElementType.Fire => new Color(1f, 0.3f, 0.2f),
            ElementType.Water => new Color(0.2f, 0.6f, 1f),
            ElementType.Electric => new Color(1f, 0.9f, 0.2f),
            ElementType.Wind => new Color(0.3f, 1f, 0.5f),
            _ => Color.gray
        };
    }
}
