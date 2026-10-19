using System;
using System.Collections;
using System.Collections.Generic;
using FourGuardians.CourseContent.Movement;
using Hwanjo.MapTools;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// 지정된 일곱 발판만 실행 중 붕괴·복원
    /// 원본 타일 데이터는 삭제하지 않고, 실행 중 타일 색과 실행용 Collider만 변경
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class MapCollapsingPlatforms : MonoBehaviour
    {
        [Serializable]
        public sealed class Platform
        {
            public Tilemap map;
            public Vector3Int firstCell;
            public Vector3Int lastCell;

            //실행 중에만 사용하는 상태 (직렬화하지 않음)
            [NonSerialized] public BoxCollider2D surface;
            [NonSerialized] public bool collapsed, wasStanding, visited;
            [NonSerialized] public Color[] colors;
            [NonSerialized] public TileFlags[] flags;
        }

        public ExcelTilemapSource source;
        public BasicPlayerMovement2D player;
        public MapOneWayPlatforms2D generatedPlatforms;
        public Platform[] platforms;
        [Min(1f)] public float restoreDuration = 18f;
        public SpriteRenderer switchVisual;

        public bool Ready { get; private set; }
        public float Remaining { get; private set; }
        public int RestoreCount { get; private set; }
        public int CollapsedCount
        {
            get
            {
                int count = 0;
                foreach (var p in platforms)
                    if (p.collapsed) count++;
                return count;
            }
        }

        private Collider2D playerCollider;
        private bool restoring;
        private Color switchColor;

        private IEnumerator Start()
        {
            playerCollider = player.GetComponent<Collider2D>();
            switchColor = switchVisual != null ? switchVisual.color : Color.white;

            //원본 OneWay 타일맵의 실행용 Collider가 만들어진 뒤에 범위를 연결하기 위해 한 프레임 대기
            yield return null;

            Ready = Bind();
            if (!Ready) Debug.LogError("Collapse group could not bind all seven authored one-way platforms.");
        }

        private bool Bind()
        {
            int bound = 0;
            foreach (var p in platforms)
            {
                //복원 시 되돌릴 원래 타일 색과 플래그 보관
                int count = p.lastCell.x - p.firstCell.x + 1;
                p.colors = new Color[count];
                p.flags = new TileFlags[count];
                for (int i = 0; i < count; i++)
                {
                    Vector3Int cell = p.firstCell + Vector3Int.right * i;
                    p.colors[i] = p.map.GetColor(cell);
                    p.flags[i] = p.map.GetTileFlags(cell);
                }

                //좌우 끝과 윗면 높이가 같은 실행용 발판 Collider를 연결
                Vector3 corner = p.map.CellToWorld(p.firstCell);
                float left = corner.x, right = left + count * source.cellSize, top = corner.y + source.cellSize;
                foreach (var surface in generatedPlatforms.Surfaces)
                {
                    Bounds b = surface.bounds;
                    if (Mathf.Abs(b.min.x - left) < .02f && Mathf.Abs(b.max.x - right) < .02f && Mathf.Abs(b.max.y - top) < .02f)
                    {
                        p.surface = surface;
                        bound++;
                        break;
                    }
                }
            }
            return bound == platforms.Length;
        }

        private void FixedUpdate()
        {
            if (!Ready || playerCollider == null) return;

            //문 연출·일시정지·게임오버처럼 Playing이 아닌 동안에는 붕괴와 복구 시간을 멈춤
            bool paused = GameManager.HasInstance && GameManager.Instance.CurrentState != GameState.Playing;
            if (paused) return;

            if (restoring)
            {
                Remaining = Mathf.Max(0f, Remaining - Time.fixedDeltaTime);
                if (Remaining <= 0f) restoring = false;
            }

            foreach (var p in platforms)
            {
                bool standing = StandingOn(p);
                if (standing) p.visited = true;

                //밟았던 발판에서 내려오면 그 발판만 붕괴
                if (!restoring && !p.collapsed && p.visited && p.wasStanding && !standing)
                    SetCollapsed(p, true);

                //복구 시간이 끝나도 지금 밟고 있는 발판은 바로 지우지 않음. 내려오면 붕괴하여 재시도할 수 있음
                if (!restoring && Remaining <= 0f && RestoreCount > 0 && !standing && !p.collapsed)
                    SetCollapsed(p, true);

                p.wasStanding = standing;
            }

            //복구 중에는 남은 시간에 따라 스위치 색을 바꿔 표시
            if (switchVisual != null)
                switchVisual.color = restoring
                    ? Color.Lerp(new Color(1f, .65f, .2f), new Color(.3f, 1f, .7f), Remaining / restoreDuration)
                    : switchColor;
        }

        private bool StandingOn(Platform p)
        {
            if (p.surface == null || p.collapsed || !p.surface.enabled) return false;

            Bounds body = playerCollider.bounds, floor = p.surface.bounds;
            return player.IsGrounded && body.max.x > floor.min.x + .02f && body.min.x < floor.max.x - .02f &&
                Mathf.Abs(body.min.y - floor.max.y) < .12f;
        }

        /// <summary> 복구 스위치 공격 시 호출. 다시 공격하면 제한 시간을 처음부터 다시 채움 </summary>
        public void RestoreTemporarily()
        {
            if (!Ready) return;

            RestoreCount++;
            restoring = true;
            Remaining = restoreDuration;
            foreach (var p in platforms)
            {
                SetCollapsed(p, false);
                p.wasStanding = false;
                p.visited = false;
            }
        }

        private void SetCollapsed(Platform p, bool collapsed)
        {
            p.collapsed = collapsed;
            if (p.surface != null) p.surface.enabled = !collapsed;

            //타일에 색 잠금 플래그가 있으면 SetColor가 무시되므로 먼저 해제. 붕괴 시에는 투명도만 0으로 변경
            for (int i = 0; i < p.colors.Length; i++)
            {
                Vector3Int cell = p.firstCell + Vector3Int.right * i;
                p.map.SetTileFlags(cell, TileFlags.None);
                Color color = p.colors[i];
                if (collapsed) color.a = 0f;
                p.map.SetColor(cell, color);
            }
        }

        private void OnDisable()
        {
            if (platforms == null) return;

            //비활성화·씬 종료 시 실행용 Collider와 타일 색·플래그를 원래 상태로 복구
            foreach (var p in platforms)
            {
                if (p.surface != null) p.surface.enabled = true;
                if (p.map == null || p.colors == null) continue;

                for (int i = 0; i < p.colors.Length; i++)
                {
                    Vector3Int cell = p.firstCell + Vector3Int.right * i;
                    p.map.SetTileFlags(cell, TileFlags.None);
                    p.map.SetColor(cell, p.colors[i]);
                    p.map.SetTileFlags(cell, p.flags[i]);
                }
            }
        }
    }
}
