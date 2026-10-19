using System;
using FourGuardians.CourseContent.Combat;
using FourGuardians.CourseContent.Movement;
using Hwanjo.MapTools;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// Excel 피해 표시 타일에 닿으면 피해를 주고 마지막 안전 위치로 되돌림
    /// 치명 피해 이후의 사망·부활·저장은 기존 시스템에 맡김
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(BasicPlayerMovement2D), typeof(Health2D))]
    public sealed class MapSafety2D : MonoBehaviour
    {
        public ExcelTilemapSource source;
        [Min(1)] public int damage = 1;
        [Min(0.1f)] public float returnCooldown = 0.6f;
        [Min(0f)] public float safeMargin = 0.25f;

        public event Action Returned;
        public Vector2 LastSafePosition { get; private set; }
        public int ReturnCount { get; private set; }

        private BasicPlayerMovement2D movement;
        private Health2D health;
        private Rigidbody2D body;
        private CapsuleCollider2D capsule;
        private MapPlayerAutoSave autoSave;
        private Vector2 initialPosition, previousCenter;
        private float cooldown, stableTime, previousSpeed;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        private readonly Collider2D[] overlaps = new Collider2D[16];

        private void Awake()
        {
            movement = GetComponent<BasicPlayerMovement2D>();
            health = GetComponent<Health2D>();
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            autoSave = GetComponent<MapPlayerAutoSave>();
            initialPosition = LastSafePosition = body.position;
            previousCenter = capsule.bounds.center;
        }

        private void OnEnable()
        {
            if (capsule != null) previousCenter = capsule.bounds.center;
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        //팀의 기존 Death 상태와 애니메이션만 연결. 리스폰 시점/위치/HP 회복은 결정하지 않음
        private void OnDied()
        {
            movement.EnterDeathState();
        }

        private void FixedUpdate()
        {
            if (source == null) return;

            cooldown = Mathf.Max(0f, cooldown - Time.fixedDeltaTime);
            Vector2 center = capsule.bounds.center;
            float speed = body.linearVelocity.magnitude;
            float expectedTravel = Mathf.Max(1.5f, Mathf.Max(speed, previousSpeed) * Time.fixedDeltaTime * 2f + 0.5f);
            previousSpeed = speed;
            bool dead = health.IsDead || movement.Action == WarriorAction.Death;

            //치명 피해는 기존 팀의 사망 이벤트에 맡김. 이 컴포넌트는 부활/저장/씬 전환을 결정하지 않음
            if (dead)
            {
                previousCenter = center;
                return;
            }

            //저장 복구·기믹 귀환처럼 외부에서 순간 이동했으면 이전 위치부터 쓸어 검사하지 않고 도착 위치만 검사
            if ((center - previousCenter).sqrMagnitude > expectedTravel * expectedTravel)
            {
                previousCenter = center;
                initialPosition = body.position;
                stableTime = 0f;
            }

            bool touched = cooldown <= 0f && SweptHazard(previousCenter, center);
            previousCenter = center;
            if (touched)
            {
                health.TakeDamage(new DamageInfo(damage, center, Vector2.zero, source.gameObject));
                if (!health.IsDead) ReturnToSafe();
                return;
            }

            //자유 이동 상태로 안전한 바닥에 잠시 머물렀을 때만 복귀 위치로 기록
            bool free = movement.Action is WarriorAction.Idle or WarriorAction.Run or WarriorAction.Crouch;
            if (cooldown <= 0f && free && movement.IsGrounded && !movement.IsDroppingThrough && Mathf.Abs(body.linearVelocity.y) < 0.15f && IsSafe(body.position))
            {
                stableTime += Time.fixedDeltaTime;
                if (stableTime >= 0.12f) LastSafePosition = body.position;
            }
            else stableTime = 0f;
        }

        //빠르게 움직여도 얇은 피해 줄을 건너뛰지 않도록 반 셀 간격으로 나눠 검사
        private bool SweptHazard(Vector2 from, Vector2 to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / Mathf.Max(0.02f, source.cellSize * 0.5f)));
            for (int i = 0; i <= steps; i++)
                if (TouchesHazard(Vector2.Lerp(from, to, (float)i / steps), 0f)) return true;
            return false;
        }

        //캡슐을 반지름 + 세로 선분으로 보고, 주변 피해 셀 사각형과의 거리로 접촉 판정
        private bool TouchesHazard(Vector2 center, float margin)
        {
            Vector2 size = capsule.bounds.size;
            float radius = size.x * 0.5f + margin;
            float segment = Mathf.Max(0f, (size.y - size.x) * 0.5f);
            Vector2 extent = new Vector2(radius, segment + radius);
            foreach (var region in source.regions)
            {
                var map = region.damage;
                if (map == null) continue;

                Vector3Int first = map.WorldToCell(center - extent), last = map.WorldToCell(center + extent);
                for (int y = first.y; y <= last.y; y++)
                    for (int x = first.x; x <= last.x; x++)
                    {
                        var cell = new Vector3Int(x, y, 0);
                        if (!map.HasTile(cell)) continue;

                        Vector3 min = map.CellToWorld(cell);
                        Vector3 max = min + Vector3.one * source.cellSize;
                        float dx = Mathf.Max(min.x - center.x, 0f, center.x - max.x);
                        float dy = Mathf.Max(min.y - (center.y + segment), 0f, (center.y - segment) - max.y);
                        if (dx * dx + dy * dy <= radius * radius) return true;
                    }
            }
            return false;
        }

        //피해 타일과 여유 거리 밖이고, 몸이 다른 충돌체와 겹치지 않으며, 발 양쪽 아래에 바닥이 있으면 안전
        private bool IsSafe(Vector2 position)
        {
            Vector2 center = position + (Vector2)capsule.bounds.center - body.position;
            if (TouchesHazard(center, safeMargin)) return false;

            int occupied = Physics2D.OverlapCapsule(center + Vector2.up * 0.03f, (Vector2)capsule.bounds.size - Vector2.up * 0.06f,
                CapsuleDirection2D.Vertical, 0f, new ContactFilter2D { useTriggers = false }, overlaps);
            for (int i = 0; i < occupied; i++)
                if (overlaps[i].attachedRigidbody != body) return false;

            float foot = center.y - capsule.bounds.extents.y;
            float half = capsule.bounds.extents.x * 0.7f;
            return HasFloor(new Vector2(center.x - half, foot + 0.035f)) && HasFloor(new Vector2(center.x + half, foot + 0.035f));
        }

        private bool HasFloor(Vector2 origin)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.Raycast(origin, Vector2.down, filter, hits, 0.12f);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && hits[i].collider.attachedRigidbody != body && hits[i].normal.y > 0.6f) return true;
            return false;
        }

        private void ReturnToSafe()
        {
            //마지막 발판이 붕괴 등으로 사라졌으면 맵 시작점보다 먼저 자동저장의 영구 지면 위치를 사용
            Vector2 destination = IsSafe(LastSafePosition) ? LastSafePosition
                : autoSave != null && IsSafe(autoSave.LastPermanentPosition) ? autoSave.LastPermanentPosition
                : initialPosition;
            movement.ResetAfterTeleport(destination + Vector2.up * 0.015f);
            Physics2D.SyncTransforms();
            previousCenter = capsule.bounds.center;
            cooldown = returnCooldown;
            stableTime = 0f;
            ReturnCount++;
            Returned?.Invoke();
        }
    }
}
