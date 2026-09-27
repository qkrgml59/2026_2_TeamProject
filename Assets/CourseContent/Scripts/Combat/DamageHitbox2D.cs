using System.Collections.Generic;
using UnityEngine;

namespace FourGuardians.CourseContent.Combat
{
    /// <summary>공격 프레임 동안만 켜지며, 한 번 켤 때 대상 하나를 한 번만 타격합니다.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class DamageHitbox2D : MonoBehaviour
    {
        [Tooltip("이 공격을 소유한 팀입니다.")]
        [SerializeField] private CombatTeam ownerTeam;
        [Tooltip("한 번 적중할 때 감소시킬 체력입니다.")]
        [SerializeField, Min(1)] private int damage = 1;
        [Tooltip("맞은 대상을 공격 방향으로 밀어내는 속도입니다.")]
        [SerializeField] private Vector2 knockback = new Vector2(4f, 2f);

        private readonly HashSet<Hurtbox2D> hitTargets = new HashSet<Hurtbox2D>();
        private Collider2D hitboxCollider;
        private int direction = 1;

        public void Configure(CombatTeam team, int newDamage, Vector2 newKnockback)
        {
            ownerTeam = team;
            damage = Mathf.Max(1, newDamage);
            knockback = newKnockback;
        }

        private void Awake()
        {
            hitboxCollider = GetComponent<Collider2D>();
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
        }

        public void BeginAttack(int attackDirection)
        {
            direction = attackDirection >= 0 ? 1 : -1;
            hitTargets.Clear();
            hitboxCollider.enabled = true;
        }

        public void EndAttack()
        {
            if (hitboxCollider != null)
            {
                hitboxCollider.enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Hurtbox2D hurtbox = other.GetComponent<Hurtbox2D>();

            if (hurtbox == null || hurtbox.IsDead || hurtbox.Team == ownerTeam || !hitTargets.Add(hurtbox))
            {
                return;
            }

            Vector2 directedKnockback = new Vector2(Mathf.Abs(knockback.x) * direction, knockback.y);
            DamageInfo damageInfo = new DamageInfo(damage, other.ClosestPoint(transform.position), directedKnockback, gameObject);
            hurtbox.Receive(damageInfo);
        }
    }
}
