using UnityEngine;

namespace FourGuardians.CourseContent.Combat
{
    /// <summary>맞을 수 있는 영역입니다. 실제 체력 계산은 Health2D에 위임합니다.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hurtbox2D : MonoBehaviour
    {
        [Tooltip("피해를 전달받을 체력 컴포넌트입니다.")]
        [SerializeField] private Health2D health;

        public CombatTeam Team => health == null ? CombatTeam.Neutral : health.Team;
        public bool IsDead => health == null || health.IsDead;

        public void Configure(Health2D newHealth)
        {
            health = newHealth;
        }

        public void Receive(DamageInfo damageInfo)
        {
            health?.TakeDamage(damageInfo);
        }

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
            health = GetComponentInParent<Health2D>();
        }
    }
}
