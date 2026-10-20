using System;
using FourGuardians.CourseContent.Combat;
using FourGuardians.CourseContent.Movement;
using Hwanjo.MapTools;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary> 기존 Player 저장 형식(SaveId "Player", 위치)과 자동저장 조건을 MapTest 이동 컴포넌트에 연결 </summary>
    [DefaultExecutionOrder(250)]
    [RequireComponent(typeof(BasicPlayerMovement2D), typeof(Rigidbody2D))]
    public sealed class MapPlayerAutoSave : MonoBehaviour, ISaveable, ISaveCondition
    {
        [Serializable]
        private struct State
        {
            public Vector2 position;
        }

        public ExcelTilemapSource source;
        public string SaveId => "Player";
        public bool IsSafeToSave => IsOnPermanentGround();

        /// <summary> 마지막으로 확인한 영구 지면 위치. 저장 복구 직후에는 복구된 위치 </summary>
        public Vector2 LastPermanentPosition => lastSafePosition;

        private BasicPlayerMovement2D movement;
        private Health2D health;
        private Rigidbody2D body;
        private Collider2D capsule;
        private Vector2 lastSafePosition;
        private readonly RaycastHit2D[] floorHits = new RaycastHit2D[12];

        private void Awake()
        {
            movement = GetComponent<BasicPlayerMovement2D>();
            health = GetComponent<Health2D>();
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<Collider2D>();
            lastSafePosition = body.position;
        }

        private void OnEnable()
        {
            if (SaveManager.Instance != null) SaveManager.Instance.RegisterCondition(this);
        }

        private void OnDisable()
        {
            if (SaveManager.HasInstance) SaveManager.Instance.UnregisterCondition(this);
        }

        private void FixedUpdate()
        {
            if (IsOnPermanentGround()) lastSafePosition = body.position;
        }

        private bool IsOnPermanentGround()
        {
            if (!isActiveAndEnabled || movement == null || source == null || !movement.IsGrounded ||
                movement.IsDroppingThrough || movement.Action == WarriorAction.Death || health != null && health.IsDead) return false;
            if (GameManager.HasInstance && GameManager.Instance.CurrentState != GameState.Playing) return false;

            Bounds bounds = capsule.bounds;
            Vector2 feet = new Vector2(bounds.center.x, bounds.min.y + .035f);
            int count = Physics2D.Raycast(feet, Vector2.down, new ContactFilter2D { useTriggers = false }, floorHits, .12f);
            for (int i = 0; i < count; i++)
            {
                var hit = floorHits[i];
                if (hit.collider == null || hit.normal.y < .6f) continue;

                //이동하거나 붕괴할 수 있는 발판 위에서는 이어하기 위치를 기록하지 않음. 지역 Solid 타일맵만 인정
                foreach (var region in source.regions)
                    if (region.solid != null && hit.collider.gameObject == region.solid.gameObject) return true;
            }
            return false;
        }

        public string CaptureState() => JsonUtility.ToJson(new State { position = lastSafePosition });

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            lastSafePosition = state.position;
            movement.ResetAfterTeleport(state.position);
            Physics2D.SyncTransforms();
        }
    }
}
