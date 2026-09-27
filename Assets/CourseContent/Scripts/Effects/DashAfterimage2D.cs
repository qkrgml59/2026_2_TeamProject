using UnityEngine;

namespace FourGuardians.CourseContent.Effects
{
    /// <summary>생성된 대시 잔상을 짧게 투명하게 만든 뒤 제거합니다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DashAfterimage2D : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private float lifetime;
        private float remaining;

        public void Configure(float duration)
        {
            lifetime = Mathf.Max(0.01f, duration);
            remaining = lifetime;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            remaining -= Time.deltaTime;
            Color color = spriteRenderer.color;
            color.a = Mathf.Clamp01(remaining / lifetime) * 0.45f;
            spriteRenderer.color = color;

            if (remaining <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
