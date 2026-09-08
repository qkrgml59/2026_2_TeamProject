using UnityEngine;

namespace FourGuardians.CourseContent.Movement
{
    // 이동 로직과 애니메이션 로직을 분리해 각 스크립트가 한 가지 책임만 갖게 한다.
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class WarriorAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private BasicPlayerMovement2D movement;
        [SerializeField, Min(0f)] private float wallSlideVisualOffset = 0.18f;
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private string currentStateName;

        public void Configure(BasicPlayerMovement2D controller)
        {
            movement = controller;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (movement == null)
            {
                return;
            }

            // 왼쪽을 볼 때 이미지를 뒤집으면 좌우 전용 스프라이트를 따로 만들 필요가 없다.
            spriteRenderer.flipX = movement.Facing < 0;
            UpdateVisualPosition();
            string nextStateName = GetStateName(movement.Action);

            // 같은 상태를 매 프레임 Play하면 애니메이션이 첫 프레임에서 멈추므로 변경될 때만 재생한다.
            if (nextStateName == currentStateName)
            {
                return;
            }

            currentStateName = nextStateName;
            animator.Play(nextStateName, 0, 0f);
        }

        private void UpdateVisualPosition()
        {
            // Collider는 벽에 닿아도 그림 속 투명 여백 때문에 떨어져 보이므로 Visual만 보정한다.
            bool isTouchingWallVisual = movement.Action is WarriorAction.WallSlide
                or WarriorAction.EdgeGrab
                or WarriorAction.EdgeIdle;
            float horizontalOffset = isTouchingWallVisual
                ? movement.Facing * wallSlideVisualOffset
                : 0f;

            transform.localPosition = new Vector3(horizontalOffset, 0f, 0f);
        }

        private static string GetStateName(WarriorAction action)
        {
            return action switch
            {
                WarriorAction.Idle => "Idle",
                WarriorAction.Run => "Run",
                WarriorAction.Attack1 => "Attack",
                WarriorAction.Attack2 => "Attack",
                WarriorAction.Death => "Death NoEffect",
                WarriorAction.Hurt => "Hurt NoEffect",
                WarriorAction.JumpUp => "jump",
                WarriorAction.JumpToFall => "JumptoFall",
                WarriorAction.Fall => "Fall",
                WarriorAction.Dash => "Dash NoDust",
                WarriorAction.DashAttack => "Dash-Attack NoDust",
                WarriorAction.EdgeGrab => "Edge-Grab",
                WarriorAction.EdgeIdle => "Edge-Idle",
                WarriorAction.Crouch => "Croush",
                WarriorAction.WallSlide => "WallSlide NoDust",
                WarriorAction.LadderGrab => "Ladder",
                WarriorAction.Slide => "Slide",
                _ => "Idle"
            };
        }
    }
}
