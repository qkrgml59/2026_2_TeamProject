//using UnityEngine;


//    public enum CobatTeam
//    {
//        Player,
//        Enemy,
//        Neutral
//    }

//    ///<summary>
//    ///한 번의 타격이 전달하는 값을 한 묶음으로 보관합니다.
//    /// </summary>

//    public readonly struct DamageInfo
//    {
//        public DamageInfo(int amount, Vector2 hitPoint, Vector2 knockback, GameObject source)
//        {
//            Amout = amount;
//            hitPoint = hitPoint;
//            knockback = knockback;
//            source = source;
//        }

//        public int Amount { get; }
//        public Vector2 HitPoint { get; }
//        public Vector2 Knockback { get; }
//        public GameObject Source { get; }

//    }

//    public interface IDamageable
//    {
//        CombatTeam Team { get; }
//        bool IsDead { get; }
//        void TakeDamage(DamageInfo damageInfo);
//    }


