using System.Collections.Generic;
using System.Linq;
using Hwanjo.MapTools;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// 편집용 일방향 타일은 유지하고, 실행 중에는 수평 발판별 Collider를 만들어 아래 발판까지 통과하지 않게 함
    /// (지역 전체가 하나의 Composite이면 아래로 내려가기가 지역의 모든 일방향 발판을 한꺼번에 통과함)
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(ExcelTilemapSource))]
    public sealed class MapOneWayPlatforms2D : MonoBehaviour
    {
        private readonly List<GameObject> generated = new List<GameObject>();
        private readonly List<(Collider2D collider, bool enabled)> originals = new List<(Collider2D, bool)>();
        private readonly List<(Rigidbody2D body, bool simulated)> originalBodies = new List<(Rigidbody2D, bool)>();
        private readonly List<BoxCollider2D> surfaces = new List<BoxCollider2D>();
        public IReadOnlyList<BoxCollider2D> Surfaces => surfaces;
        private bool started;

        private void OnEnable()
        {
            if (started) Build();
        }

        private void Start()
        {
            //씬의 모든 Collider OnEnable 이후에 원본 충돌체를 끔
            started = true;
            Build();
        }

        private void Build()
        {
            if (!Application.isPlaying) return;

            var source = GetComponent<ExcelTilemapSource>();
            foreach (var region in source.regions)
            {
                Tilemap map = region.oneWay;
                var root = new GameObject("Runtime OneWay surfaces");
                root.transform.SetParent(region.root, false);
                root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                generated.Add(root);

                //행별로 일방향 타일이 있는 열을 모음
                var rows = new Dictionary<int, List<int>>();
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    if (!map.HasTile(cell)) continue;
                    if (!rows.TryGetValue(cell.y, out var columns)) rows[cell.y] = columns = new List<int>();
                    columns.Add(cell.x);
                }

                //같은 행에서 연속된 셀을 발판 하나로 묶고, 셀 위쪽 절반 높이에 착지면 Collider를 만듦
                foreach (var row in rows)
                {
                    var columns = row.Value.OrderBy(x => x).ToArray();
                    for (int index = 0; index < columns.Length;)
                    {
                        int left = columns[index], right = left;
                        while (++index < columns.Length && columns[index] == right + 1) right = columns[index];

                        var go = new GameObject($"OneWay {region.id} [{left}..{right}, {row.Key}]");
                        go.layer = map.gameObject.layer;
                        go.transform.SetParent(root.transform, false);
                        Vector3 bottomLeft = map.CellToWorld(new Vector3Int(left, row.Key, 0));
                        float width = (right - left + 1) * source.cellSize;
                        go.transform.position = bottomLeft + new Vector3(width * 0.5f, source.cellSize * 0.75f, 0f);

                        var box = go.AddComponent<BoxCollider2D>();
                        box.size = new Vector2(width, source.cellSize * 0.5f);
                        box.usedByEffector = true;
                        var effector = go.AddComponent<PlatformEffector2D>();
                        effector.useColliderMask = false;
                        effector.useOneWay = true;
                        effector.useOneWayGrouping = true;
                        effector.surfaceArc = 180f;
                        effector.useSideFriction = false;
                        effector.useSideBounce = false;
                        surfaces.Add(box);
                    }
                }

                foreach (var collider in new Collider2D[] { map.GetComponent<TilemapCollider2D>(), map.GetComponent<CompositeCollider2D>() })
                {
                    if (collider == null) continue;
                    originals.Add((collider, collider.enabled));
                    collider.enabled = false;
                }
                var originalBody = map.GetComponent<Rigidbody2D>();
                if (originalBody != null)
                {
                    //Composite의 자동 갱신과 무관하게 원본 물리 묶음 전체를 시뮬레이션에서 제외
                    originalBodies.Add((originalBody, originalBody.simulated));
                    originalBody.simulated = false;
                }
            }
            Physics2D.SyncTransforms();
        }

        private void OnDisable()
        {
            //Destroy는 프레임 끝에 처리되므로 먼저 비활성화해 바로 충돌에서 빠지게 함
            foreach (var root in generated)
                if (root != null)
                {
                    root.SetActive(false);
                    Destroy(root);
                }
            foreach (var entry in originals)
                if (entry.collider != null) entry.collider.enabled = entry.enabled;
            foreach (var entry in originalBodies)
                if (entry.body != null) entry.body.simulated = entry.simulated;
            generated.Clear();
            originals.Clear();
            originalBodies.Clear();
            surfaces.Clear();
        }
    }
}
