using FourGuardians.CourseContent.Combat;
using UnityEngine;

namespace FourGuardians.CourseContent.AI
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack,
        Hurt,
        Dead
    }

    /// <summary>순찰, 추적, 공격, 피격, 사망을 한눈에 볼 수 있는 수업용 적 AI입니다.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health2D))]
    public sealed class EnemyCombatAI2D : MonoBehaviour
    {
        [Header("대상과 이동")]
        [Tooltip("추적하고 공격할 플레이어입니다.")]
        [SerializeField] private Transform target;
        [Tooltip("순찰과 추적에 사용하는 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [Tooltip("시작 위치에서 좌우로 순찰할 거리입니다.")]
        [SerializeField, Min(0.5f)] private float patrolDistance = 3f;

        [Header("판단 거리")]
        [Tooltip("플레이어가 이 거리 안에 들어오면 추적합니다.")]
        [SerializeField, Min(0f)] private float detectionDistance = 7f;
        [Tooltip("플레이어가 이 거리 안에 들어오면 공격합니다.")]
        [SerializeField, Min(0f)] private float attackDistance = 1.35f;
        [Tooltip("공격을 다시 사용할 수 있을 때까지 기다리는 시간입니다.")]
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.2f;

        [Header("전투 연결")]
        [SerializeField] private DamageHitbox2D attackHitbox;
        [SerializeField] private SpriteRenderer visual;

        private Rigidbody2D body;
        private Health2D health;
        private float spawnX;
        private float attackTimer;
        private float attackElapsed;
        private int facing = -1;
        private bool attackHitboxActive;

        public EnemyState State { get; private set; } = EnemyState.Patrol;

        public void Configure(Transform newTarget, DamageHitbox2D newHitbox, SpriteRenderer newVisual)
        {
            target = newTarget;
            attackHitbox = newHitbox;
            visual = newVisual;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health2D>();
            spawnX = transform.position.x;
        }

        private void FixedUpdate()
        {
            attackTimer = Mathf.Max(0f, attackTimer - Time.fixedDeltaTime);

            if (health.IsDead)
            {
                EnterDeadState();
                return;
            }

            if (target == null)
            {
                Patrol();
                return;
            }

            float horizontalDistance = Mathf.Abs(target.position.x - transform.position.x);

            if (State == EnemyState.Attack)
            {
                UpdateAttack();
            }
            else if (horizontalDistance <= attackDistance && attackTimer <= 0f)
            {
                BeginAttack();
            }
            else if (horizontalDistance <= detectionDistance)
            {
                Chase();
            }
            else
            {
                Patrol();
            }

            if (visual != null)
            {
                visual.flipX = facing > 0;
            }
        }

        private void Patrol()
        {
            State = EnemyState.Patrol;

            if (transform.position.x >= spawnX + patrolDistance)
            {
                facing = -1;
            }
            else if (transform.position.x <= spawnX - patrolDistance)
            {
                facing = 1;
            }

            body.linearVelocity = new Vector2(facing * moveSpeed, body.linearVelocity.y);
        }

        private void Chase()
        {
            State = EnemyState.Chase;
            facing = target.position.x >= transform.position.x ? 1 : -1;
            body.linearVelocity = new Vector2(facing * moveSpeed, body.linearVelocity.y);
        }

        private void BeginAttack()
        {
            State = EnemyState.Attack;
            attackElapsed = 0f;
            facing = target.position.x >= transform.position.x ? 1 : -1;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            attackHitbox.transform.localPosition = new Vector3(facing * 0.75f, 0.65f, 0f);
        }

        private void UpdateAttack()
        {
            attackElapsed += Time.fixedDeltaTime;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            // 0.25~0.45초만 판정을 켜서 준비 동작과 후딜레이를 구분한다.
            if (attackElapsed >= 0.25f && attackElapsed <= 0.45f && !attackHitboxActive)
            {
                attackHitboxActive = true;
                attackHitbox.BeginAttack(facing);
            }
            else if (attackElapsed > 0.45f && attackHitboxActive)
            {
                attackHitboxActive = false;
                attackHitbox.EndAttack();
            }

            if (attackElapsed < 0.75f)
            {
                return;
            }

            attackTimer = attackCooldown;
            State = EnemyState.Chase;
        }

        private void EnterDeadState()
        {
            if (State == EnemyState.Dead)
            {
                return;
            }

            State = EnemyState.Dead;
            body.linearVelocity = Vector2.zero;
            attackHitbox.EndAttack();

            if (visual != null)
            {
                visual.color = new Color(0.25f, 0.25f, 0.3f, 0.7f);
                visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            Destroy(gameObject, 1.5f);
        }

        private void OnDisable()
        {
            attackHitbox?.EndAttack();
        }
    }
}
