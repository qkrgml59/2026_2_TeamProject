using FourGuardians.CourseContent.Movement;
using UnityEngine;

namespace FourGuardians.CourseContent.Effects
{
    /// <summary>대시 중 캐릭터 잔상을 만들고 시작 순간에 먼지 입자를 발생시킵니다.</summary>
    public sealed class DashEffect2D : MonoBehaviour
    {
        [SerializeField] private BasicPlayerMovement2D movement;
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private ParticleSystem dustParticles;
        [Tooltip("대시 중 잔상을 생성하는 시간 간격입니다.")]
        [SerializeField, Min(0.01f)] private float afterimageInterval = 0.055f;
        [Tooltip("잔상 하나가 완전히 사라질 때까지 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float afterimageLifetime = 0.22f;

        private WarriorAction previousAction;
        private float afterimageTimer;

        public void Configure(
            BasicPlayerMovement2D newMovement,
            SpriteRenderer newSourceRenderer,
            ParticleSystem newDustParticles)
        {
            movement = newMovement;
            sourceRenderer = newSourceRenderer;
            dustParticles = newDustParticles;
        }

        private void Update()
        {
            if (movement == null || sourceRenderer == null)
            {
                return;
            }

            bool isDashing = movement.Action is WarriorAction.Dash or WarriorAction.DashAttack;
            bool dashStarted = isDashing && previousAction is not WarriorAction.Dash and not WarriorAction.DashAttack;

            if (dashStarted)
            {
                dustParticles?.Emit(8);
                afterimageTimer = 0f;
            }

            if (isDashing)
            {
                afterimageTimer -= Time.deltaTime;

                if (afterimageTimer <= 0f)
                {
                    CreateAfterimage();
                    afterimageTimer = afterimageInterval;
                }
            }

            previousAction = movement.Action;
        }

        private void CreateAfterimage()
        {
            GameObject ghost = new GameObject("Dash_Afterimage", typeof(SpriteRenderer), typeof(DashAfterimage2D));
            ghost.transform.SetPositionAndRotation(sourceRenderer.transform.position, sourceRenderer.transform.rotation);
            ghost.transform.localScale = sourceRenderer.transform.lossyScale;

            SpriteRenderer ghostRenderer = ghost.GetComponent<SpriteRenderer>();
            ghostRenderer.sprite = sourceRenderer.sprite;
            ghostRenderer.flipX = sourceRenderer.flipX;
            ghostRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            ghostRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            ghostRenderer.color = new Color(0.25f, 0.85f, 1f, 0.45f);
            ghost.GetComponent<DashAfterimage2D>().Configure(afterimageLifetime);
        }
    }
}
