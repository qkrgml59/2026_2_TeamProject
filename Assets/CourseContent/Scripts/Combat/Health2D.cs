using System;
using UnityEngine;

namespace FourGuardians.CourseContent.Combat
{
    /// <summary>체력, 무적 시간, 넉백과 사망 여부만 담당하는 공통 컴포넌트입니다.</summary>
    public sealed class Health2D : MonoBehaviour, IDamageable
    {
        [Tooltip("게임을 시작할 때의 체력과 회복 가능한 최대 체력입니다.")]
        [SerializeField, Min(1)] private int maxHealth = 5;
        [Tooltip("같은 팀의 Hitbox끼리는 피해를 주지 않습니다.")]
        [SerializeField] private CombatTeam team = CombatTeam.Enemy;
        [Tooltip("한 번 맞은 직후 연속 충돌로 체력이 여러 번 줄어드는 것을 막는 시간입니다.")]
        [SerializeField, Min(0f)] private float invincibleDuration = 0.25f;

        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Color originalColor = Color.white;
        private float invincibleTimer;

        public event Action<int, int> HealthChanged;
        public event Action Died;

        public CombatTeam Team => team;
        public bool IsDead { get; private set; }
        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;

        public void Configure(int newMaxHealth, CombatTeam newTeam)
        {
            maxHealth = Mathf.Max(1, newMaxHealth);
            team = newTeam;
            CurrentHealth = maxHealth;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            visual = GetComponentInChildren<SpriteRenderer>();

            if (visual != null)
            {
                originalColor = visual.color;
            }

            CurrentHealth = maxHealth;
        }

        private void Update()
        {
            invincibleTimer = Mathf.Max(0f, invincibleTimer - Time.deltaTime);

            if (visual != null && !IsDead)
            {
                // 무적 시간 동안 짧게 색을 번갈아 보여 피격 사실을 즉시 알린다.
                bool showHitColor = invincibleTimer > 0f && Mathf.FloorToInt(invincibleTimer * 20f) % 2 == 0;
                visual.color = showHitColor ? new Color(1f, 0.45f, 0.35f) : originalColor;
            }
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (IsDead || invincibleTimer > 0f || damageInfo.Amount <= 0)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - damageInfo.Amount);
            invincibleTimer = invincibleDuration;

            if (body != null)
            {
                // AddForce 대신 속도 변화로 처리해 수업 중 결과를 예측하기 쉽게 만든다.
                body.linearVelocity += damageInfo.Knockback;
            }

            HealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth == 0)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }
    }
}
