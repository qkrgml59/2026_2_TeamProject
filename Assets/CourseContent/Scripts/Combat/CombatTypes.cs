using UnityEngine;

namespace FourGuardians.CourseContent.Combat
{
    public enum CombatTeam
    {
        Player,
        Enemy,
        Neutral
    }

    /// <summary>한 번의 타격이 전달하는 값을 한 묶음으로 보관합니다.</summary>
    public readonly struct DamageInfo
    {
        public DamageInfo(int amount, Vector2 hitPoint, Vector2 knockback, GameObject source)
        {
            Amount = amount;
            HitPoint = hitPoint;
            Knockback = knockback;
            Source = source;
        }

        public int Amount { get; }
        public Vector2 HitPoint { get; }
        public Vector2 Knockback { get; }
        public GameObject Source { get; }
    }

    public interface IDamageable
    {
        CombatTeam Team { get; }
        bool IsDead { get; }
        void TakeDamage(DamageInfo damageInfo);
    }
}
