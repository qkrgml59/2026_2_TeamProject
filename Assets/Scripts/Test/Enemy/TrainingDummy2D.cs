using System;
using FourGuardians.CourseContent.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FourGuardians.CourseContent.AI
{
    [RequireComponent(typeof(Health2D))]
    public sealed class TrainingDummy2D : MonoBehaviour
    {
        [Header("더미 원소 속성 설정")]
        public ElementType currentElement = ElementType.None;

        [Header("더미 세팅")]
        [SerializeField] private bool autoResetHealth = true;

        [Header("시각 연출 참조")]
        [SerializeField] private SpriteRenderer visual;

        [Header("속성별 색상 설정")]
        [SerializeField] private Color colorNone = Color.white;
        [SerializeField] private Color colorFire = new Color(1f, 0.4f, 0.4f);       //빨강 (불)
        [SerializeField] private Color colorWater = new Color(0.4f, 0.6f, 1f);      //파랑 (물)
        [SerializeField] private Color colorElectric = new Color(1f, 0.9f, 0.3f);   //노랑 (전기)
        [SerializeField] private Color colorWind = new Color(0.4f, 1f, 0.5f);       //초록 (바람)

        private Health2D health;

        private void Awake()
        {
            health = GetComponent<Health2D>();

            if (visual == null)
            {
                visual = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Start()
        {
            UpdateDummyVisual();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                CycleNextElement();
            }
        }

        private void CycleNextElement()
        {
            int nextIndex = ((int)currentElement + 1) % Enum.GetValues(typeof(ElementType)).Length;
            currentElement = (ElementType)nextIndex;

            UpdateDummyVisual();

            Debug.Log($"<color=cyan>[Training Dummy]</color> 더미 속성 변경 완료: <b>{currentElement}</b>");
        }

        private void UpdateDummyVisual()
        {
            if (visual == null) return;

            Color targetColor = colorNone;

            switch (currentElement)
            {
                case ElementType.Fire: targetColor = colorFire; break;
                case ElementType.Water: targetColor = colorWater; break;
                case ElementType.Electric: targetColor = colorElectric; break;
                case ElementType.Wind: targetColor = colorWind; break;
                default: targetColor = colorNone; break;
            }

            visual.color = targetColor;

            if (health != null)
            {
                var healthOriginalColorField = typeof(Health2D).GetField("originalColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (healthOriginalColorField != null)
                {
                    healthOriginalColorField.SetValue(health, targetColor);
                }
            }
        }

        private void HandleDied()
        {
            Debug.Log("<color=cyan>[Training Dummy]</color> 더미 체력 0 도달! 체력을 재설정합니다.");

            if (autoResetHealth && health != null)
            {
                health.Configure(health.MaxHealth, health.Team);
            }
        }

        private void OnEnable()
        {
            if (health != null) health.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= HandleDied;
        }
    }
}