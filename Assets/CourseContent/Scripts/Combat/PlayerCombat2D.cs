using FourGuardians.CourseContent.Movement;
using UnityEngine;

namespace FourGuardians.CourseContent.Combat
{
    /// <summary>플레이어 행동 상태를 공격 판정의 활성 프레임으로 변환합니다.</summary>
    public sealed class PlayerCombat2D : MonoBehaviour
    {
        [Tooltip("현재 Attack 상태를 읽을 이동 컴포넌트입니다.")]
        [SerializeField] private BasicPlayerMovement2D movement;
        [Tooltip("공격 중 실제 충돌을 검사할 Hitbox입니다.")]
        [SerializeField] private DamageHitbox2D attackHitbox;
        [Tooltip("플레이어 중심에서 공격 판정 중심까지의 거리입니다.")]
        [SerializeField, Min(0f)] private float attackOffset = 0.8f;

        private WarriorAction previousAction;
        private float attackElapsed;
        private bool hitboxActive;

        public void Configure(BasicPlayerMovement2D newMovement, DamageHitbox2D newHitbox)
        {
            movement = newMovement;
            attackHitbox = newHitbox;
        }

        private void Update()
        {
            if (movement == null || attackHitbox == null)
            {
                return;
            }

            WarriorAction action = movement.Action;
            bool isAttack = action is WarriorAction.BasicAttack
                or WarriorAction.Attack1
                or WarriorAction.Attack2
                or WarriorAction.DashAttack
                or WarriorAction.JumpAttack;

            if (!isAttack)
            {
                StopHitbox();
                previousAction = action;
                return;
            }

            if (action != previousAction)
            {
                attackElapsed = 0f;
                StopHitbox();
            }

            attackElapsed += Time.deltaTime;
            previousAction = action;
            attackHitbox.transform.localPosition = new Vector3(movement.Facing * attackOffset, 0.7f, 0f);

            // 공격 애니메이션의 준비 동작이 끝난 뒤 검을 휘두르는 구간에만 판정을 켠다.
            bool usesDashAttackMotion = action is WarriorAction.BasicAttack
                or WarriorAction.DashAttack
                or WarriorAction.JumpAttack;
            float activeStart = usesDashAttackMotion ? 0.08f : 0.22f;
            float activeEnd = usesDashAttackMotion ? 0.32f : 0.58f;
            bool shouldBeActive = attackElapsed >= activeStart && attackElapsed <= activeEnd;

            if (shouldBeActive && !hitboxActive)
            {
                hitboxActive = true;
                attackHitbox.BeginAttack(movement.Facing);
            }
            else if (!shouldBeActive && hitboxActive)
            {
                StopHitbox();
            }
        }

        private void StopHitbox()
        {
            hitboxActive = false;
            attackHitbox?.EndAttack();
        }

        private void OnDisable()
        {
            StopHitbox();
        }
    }
}
