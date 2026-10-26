using System;
using UnityEngine;

namespace FourGuardians.CourseContent.Combat
{
    /// <summary>
    /// 체력, 무적 시간, 넉백, 사망 여부와 데미지 숫자를 관리합니다.
    /// </summary>
    public sealed class Health2D : MonoBehaviour, IDamageable
    {
        [Header("체력 설정")]
        [Tooltip("게임을 시작할 때의 체력과 회복 가능한 최대 체력입니다.")]
        [SerializeField, Min(1)] private int maxHealth = 5;

        [Tooltip("같은 팀의 Hitbox끼리는 피해를 주지 않습니다.")]
        [SerializeField] private CombatTeam team = CombatTeam.Enemy;

        [Tooltip("피격 후 연속으로 피해를 받지 않도록 하는 시간입니다.")]
        [SerializeField, Min(0f)] private float invincibleDuration = 0.25f;

        [Header("데미지 숫자")]
        [Tooltip("피격 시 생성할 데미지 숫자 프리팹입니다.")]
        [SerializeField] private DamagePopup damagePopupPrefab;

        [Tooltip("적의 중심에서 숫자를 위로 띄울 거리입니다.")]
        [SerializeField] private float popupOffsetY = 0.8f;

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
            invincibleTimer = Mathf.Max(
                0f,
                invincibleTimer - Time.deltaTime
            );

            if (visual != null && !IsDead)
            {
                // 무적 시간 동안 색상을 번갈아 표시합니다.
                bool showHitColor =
                    invincibleTimer > 0f &&
                    Mathf.FloorToInt(invincibleTimer * 20f) % 2 == 0;

                visual.color = showHitColor
                    ? new Color(1f, 0.45f, 0.35f)
                    : originalColor;
            }
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            // 사망했거나 무적 상태라면 피해를 받지 않습니다.
            if (IsDead ||
                invincibleTimer > 0f ||
                damageInfo.Amount <= 0)
            {
                return;
            }

            // 실제로 감소하는 체력을 계산합니다.
            int actualDamage = Mathf.Min(
                CurrentHealth,
                damageInfo.Amount
            );

            // 체력을 감소시킵니다.
            CurrentHealth = Mathf.Max(
                0,
                CurrentHealth - actualDamage
            );

            // 무적 시간을 적용합니다.
            invincibleTimer = invincibleDuration;

            // 적의 중심을 기준으로 데미지 숫자를 생성합니다.
            ShowDamagePopup(actualDamage);

            // 기존 넉백 기능을 유지합니다.
            if (body != null)
            {
                body.linearVelocity += damageInfo.Knockback;
            }

            // 체력 변경 이벤트를 호출합니다.
            HealthChanged?.Invoke(CurrentHealth, maxHealth);

            // 체력이 0이면 사망 처리합니다.
            if (CurrentHealth == 0)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }

        private void ShowDamagePopup(int damage)
        {
            if (damagePopupPrefab == null)
            {
                Debug.LogWarning(
                    $"{gameObject.name}: Damage Popup Prefab이 연결되지 않았습니다.",
                    gameObject
                );

                return;
            }

            if (damage <= 0)
            {
                return;
            }

            // 적 본체의 위치를 기준으로 숫자를 생성합니다.
            Vector3 spawnPosition = transform.position
                + Vector3.up * popupOffsetY;

            // 적의 Z 좌표를 그대로 사용합니다.
            spawnPosition.z = transform.position.z;

            DamagePopup popup = Instantiate(
                damagePopupPrefab,
                spawnPosition,
                Quaternion.identity
            );

            popup.Initialize(damage);
        }
    }
}