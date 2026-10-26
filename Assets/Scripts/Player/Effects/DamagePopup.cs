using TMPro;
using UnityEngine;

namespace FourGuardians.CourseContent.Combat
{
    /// <summary>
    /// 데미지 숫자를 위로 이동시키고 점점 투명하게 만듭니다.
    /// </summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        [Header("숫자 표시")]
        [SerializeField] private TextMeshPro damageText;

        [Header("움직임 설정")]
        [SerializeField, Min(0.01f)] private float duration = 0.6f;

        [SerializeField] private float moveDistance = 0.6f;

        private float elapsedTime;
        private Vector3 startPosition;
        private Color originalColor;

        private void Awake()
        {
            // 같은 오브젝트에서 TextMeshPro를 찾습니다.
            if (damageText == null)
            {
                damageText = GetComponent<TextMeshPro>();
            }

            // 자식 오브젝트에 있는 경우에도 찾습니다.
            if (damageText == null)
            {
                damageText = GetComponentInChildren<TextMeshPro>();
            }

            if (damageText != null)
            {
                originalColor = damageText.color;
            }
            else
            {
                Debug.LogError(
                    "DamagePopup에 TextMeshPro가 연결되지 않았습니다.",
                    gameObject
                );
            }
        }

        public void Initialize(int damage)
        {
            if (damageText != null)
            {
                damageText.text = damage.ToString();
                damageText.color = originalColor;
            }

            // 생성된 위치를 시작 위치로 저장합니다.
            startPosition = transform.position;

            elapsedTime = 0f;
        }

        private void Update()
        {
            elapsedTime += Time.deltaTime;

            // 애니메이션 진행도를 계산합니다.
            float progress = Mathf.Clamp01(
                elapsedTime / duration
            );

            // 시작 위치에서 위쪽으로 이동합니다.
            transform.position = startPosition
                + Vector3.up * (moveDistance * progress);

            // 점점 투명해집니다.
            if (damageText != null)
            {
                Color color = originalColor;
                color.a = 1f - progress;

                damageText.color = color;
            }

            // 시간이 지나면 제거합니다.
            if (elapsedTime >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}