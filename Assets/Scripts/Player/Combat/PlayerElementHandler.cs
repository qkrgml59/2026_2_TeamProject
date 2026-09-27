using UnityEngine;
using UnityEngine.InputSystem;

namespace FourGuardians.CourseContent.Combat
{
    public sealed class PlayerElementHandler : MonoBehaviour
    {
        [Header("현재 플레이어 착용 원소 속성")]
        [SerializeField] private ElementType currentElement = ElementType.None;

        public ElementType CurrentElement => currentElement;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) SetElement(ElementType.Fire);
            if (keyboard.digit2Key.wasPressedThisFrame) SetElement(ElementType.Water);
            if (keyboard.digit3Key.wasPressedThisFrame) SetElement(ElementType.Electric);
            if (keyboard.digit4Key.wasPressedThisFrame) SetElement(ElementType.Wind);
            if (keyboard.digit0Key.wasPressedThisFrame) SetElement(ElementType.None);
        }

        public void SetElement(ElementType newElement)
        {
            currentElement = newElement;
            Debug.Log($"[원소 교체] 현재 속성: {currentElement}");
        }
    }
}

