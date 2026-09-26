using UnityEngine;

namespace FourGuardians.CourseContent.Movement
{
    // 이동 로직과 애니메이션 로직을 분리해 각 스크립트가 한 가지 책임만 갖게 한다.
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class WarriorAnimatorBridge : MonoBehaviour
    {
        [Tooltip("물리 상태를 읽어 Animator 상태와 좌우 반전을 결정하는 플레이어 이동 컴포넌트입니다.")]
        [SerializeField] private BasicPlayerMovement2D movement;
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
            UpdateAnimationSpeed();
            string nextStateName = GetStateName(movement.Action);

            // 같은 상태를 매 프레임 Play하면 애니메이션이 첫 프레임에서 멈추므로 변경될 때만 재생한다.
            if (nextStateName == currentStateName)
            {
                return;
            }

            currentStateName = nextStateName;
            animator.Play(nextStateName, 0, 0f);
        }

        private void UpdateAnimationSpeed()
        {
            // 사다리에 매달린 상태는 유지하면서 입력이 없을 때 프레임 재생만 멈춘다.
            // 다시 위/아래를 누르면 현재 프레임부터 자연스럽게 이어서 재생된다.
            bool stoppedOnLadder = movement.Action == WarriorAction.LadderGrab
                && Mathf.Abs(movement.VerticalInput) < 0.01f;
            if (stoppedOnLadder)
            {
                animator.speed = 0f;
            }
            else if (movement.Action == WarriorAction.Dash)
            {
                // 0.7초짜리 원본 대시를 약 0.28초 안에 끝까지 보여준다.
                animator.speed = 2.5f;
            }
            else if (movement.Action == WarriorAction.DashAttack)
            {
                // 1초짜리 대시 공격도 액션 시간에 맞춰 빠르게 재생한다.
                animator.speed = 2.2f;
            }
            else
            {
                animator.speed = 1f;
            }
        }

        private void UpdateVisualPosition()
        {
            // 캐릭터 Collider 폭에 비례한 값이므로 크기를 바꿔도 같은 비율로 벽 쪽에 붙는다.
            bool isTouchingWallVisual = movement.Action is WarriorAction.WallSlide
                or WarriorAction.EdgeGrab
                or WarriorAction.EdgeIdle;
            float horizontalOffset = isTouchingWallVisual
                ? movement.Facing * movement.WallVisualOffset
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
                WarriorAction.Dash => "Dash",
                WarriorAction.DashAttack => "Dash-Attack",
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
