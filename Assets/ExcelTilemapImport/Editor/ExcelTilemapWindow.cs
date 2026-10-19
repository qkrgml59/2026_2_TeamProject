using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hwanjo.MapTools.Editor
{
    public sealed class ExcelTilemapWindow : EditorWindow
    {
        [SerializeField] private Grid target;
        private MapSyncPlan preview;
        private string status = "Grid를 선택하고 적용 방향의 변경 셀을 먼저 확인하세요.";
        private MessageType statusType = MessageType.Info;
        private Vector2 scroll, bodyScroll;
        private int page, regionIndex, kindIndex;
        private Vector2Int editCell;
        private bool editOneCell, showEdit;

        [MenuItem("Tools/환조/Excel → Tilemap")]
        public static void Open()
        {
            var window = GetWindow<ExcelTilemapWindow>("Excel ↔ Tilemap");
            window.minSize = new Vector2(620, 500);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += Invalidate;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= Invalidate;
        }

        //Undo·설정 변경 후에는 이전 미리보기를 버려 오래된 비교 결과로 적용하지 않게 함
        private void Invalidate()
        {
            preview = null;
            Repaint();
        }

        private void OnGUI()
        {
            using (var view = new EditorGUILayout.ScrollViewScope(bodyScroll))
            {
                bodyScroll = view.scrollPosition;
                DrawGUI();
            }
        }

        //변경은 항상 "미리보기 → 확인한 변경 적용" 두 단계로만 반영
        private void DrawGUI()
        {
            EditorGUILayout.HelpBox("자동 갱신 없음. 저장된 Excel과 Unity 변경을 비교한 뒤 선택한 방향으로 적용합니다. 반대편 단독 변경은 보존하고, 같은 셀의 충돌은 중단합니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var chosen = (Grid)EditorGUILayout.ObjectField("지도 Grid", target, typeof(Grid), true);
                if (chosen != target)
                {
                    target = chosen;
                    editOneCell = false;
                    Invalidate();
                }
                if (target == null && GUILayout.Button("선택한 오브젝트의 Grid 사용")) target = Selection.activeGameObject?.GetComponentInParent<Grid>();
                if (target == null && GUILayout.Button("새 전용 Grid 만들기")) target = ExcelTilemapImporter.CreateTarget();
                var source = ExcelTilemapImporter.FindSource(target);
                if (source == null)
                {
                    EditorGUILayout.HelpBox("씬의 Excel Map Grid를 지정하세요.", MessageType.None);
                    return;
                }
                if (DrawSettings(source)) Invalidate();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Excel → Unity 적용\n변경 셀 미리보기", GUILayout.Height(42))) MakePreview(source, false);
                if (GUILayout.Button("Unity → Excel 반영\n변경 셀 미리보기", GUILayout.Height(42))) MakePreview(source, true);
                EditorGUILayout.EndHorizontal();

                if (preview != null)
                {
                    EditorGUILayout.HelpBox(preview.Summary, preview.HasConflicts ? MessageType.Error : MessageType.Info);
                    if (preview.HasConflicts) EditorGUILayout.HelpBox("충돌 셀을 Excel 또는 Unity에서 원하는 값으로 맞추고 다시 미리보세요. 충돌을 자동으로 선택하거나 덮어쓰지 않습니다.", MessageType.Warning);

                    //충돌이 하나라도 있으면 적용 버튼을 비활성화
                    using (new EditorGUI.DisabledScope(preview.HasConflicts))
                        if (GUILayout.Button(preview.Export ? "확인한 변경을 Excel에 적용" : "확인한 변경을 Unity에 적용", GUILayout.Height(28)))
                        {
                            try
                            {
                                if (preview.Export)
                                {
                                    string backup = ExcelMapSync.ApplyExport(source, preview);
                                    status = "Excel 반영 완료. 기준을 유지하려면 씬을 저장하세요." + (backup == "" ? " 변경 셀 없음." : "\n원본 백업: " + backup + "\nExcel 변경은 Unity Ctrl+Z로 되돌아가지 않습니다.");
                                }
                                else
                                {
                                    ExcelMapSync.ApplyImport(source, preview);
                                    status = "Unity 반영 완료. Ctrl+Z로 되돌릴 수 있습니다. 결과와 기준을 유지하려면 씬을 저장하세요.";
                                }
                                statusType = MessageType.Info;
                                preview = null;
                            }
                            catch (Exception error)
                            {
                                status = error.Message;
                                statusType = MessageType.Error;
                                preview = null;
                            }
                        }
                    if (preview != null) DrawChanges();
                }

                EditorGUILayout.HelpBox(status, statusType);
                showEdit = EditorGUILayout.Foldout(showEdit, "Unity에서 지역·종류를 지정해 1셀 수정", true);
                if (showEdit) DrawEdit(source);
                EditorGUILayout.HelpBox("빨강은 피해 표시만 합니다. 지역 숨김은 Hierarchy 눈 아이콘을 사용하세요. 지역 비활성화는 충돌도 끕니다.", MessageType.None);
            }
        }

        private void MakePreview(ExcelTilemapSource source, bool export)
        {
            try
            {
                preview = ExcelMapSync.Preview(source, export);
                page = 0;
                status = preview.Excel.Summary();
                statusType = MessageType.Info;
            }
            catch (Exception error)
            {
                preview = null;
                status = error.Message;
                statusType = MessageType.Error;
            }
        }

        //변경 셀은 50개씩 페이지로 보여 주고, 전체 목록은 CSV로 저장
        private void DrawChanges()
        {
            EditorGUILayout.BeginHorizontal();
            int pages = Math.Max(1, (preview.Changes.Count + 49) / 50);
            if (GUILayout.Button("이전", GUILayout.Width(45))) page = Math.Max(0, page - 1);
            GUILayout.Label($"{page+1}/{pages} · 전체 {preview.Changes.Count:N0}셀");
            if (GUILayout.Button("다음", GUILayout.Width(45))) page = Math.Min(pages - 1, page + 1);
            if (GUILayout.Button("전체 CSV 저장", GUILayout.Width(100)))
            {
                string path = EditorUtility.SaveFilePanel("변경 셀 목록", "", "MapCellChanges.csv", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    string Q(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
                    var lines = new List<string> { "셀,변경측,선택방향적용,기준,Excel,Unity" };
                    foreach (var c in preview.Changes) lines.Add(string.Join(",", new[] { Q(c.Address), Q(c.Relation), Q(c.Apply ? "적용" : "보존/충돌"), Q(c.Before), Q(c.Excel), Q(c.Unity) }));
                    File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(true));
                }
            }
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(300));
            for (int i = page * 50; i < Math.Min((page + 1) * 50, preview.Changes.Count); i++)
            {
                var c = preview.Changes[i];
                EditorGUILayout.LabelField($"{c.Address} · {c.Relation} · {(c.Apply?"적용":c.Conflict?"중단":"보존/동일")}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("기준: " + c.Before);
                EditorGUILayout.LabelField("Excel: " + c.Excel);
                EditorGUILayout.LabelField("Unity: " + c.Unity);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawEdit(ExcelTilemapSource source)
        {
            if (string.IsNullOrEmpty(source.syncBaseline) || source.regions.Count == 0)
            {
                EditorGUILayout.HelpBox("기준 지도를 먼저 가져오세요.", MessageType.None);
                return;
            }
            regionIndex = Mathf.Clamp(regionIndex, 0, source.regions.Count - 1);
            regionIndex = EditorGUILayout.Popup("지역", regionIndex, source.regions.ConvertAll(r => "Region " + r.id).ToArray());
            kindIndex = EditorGUILayout.Popup("종류", kindIndex, new[] { "Solid · 지역 땅색", "OneWay · #FF8000", "Damage 표시 · #FF0000", "Open · 타일 삭제 / 지역 배경색" });
            editCell = EditorGUILayout.Vector2IntField("Unity 셀 좌표", editCell);
            if (GUILayout.Button("이 1셀 수정")) Paint(source, new Vector3Int(editCell.x, editCell.y, 0));
            editOneCell = EditorGUILayout.ToggleLeft("Scene 뷰에서 클릭한 1셀 수정 (Alt 카메라 조작 유지)", editOneCell);
            EditorGUILayout.HelpBox("선택한 지역 안에서만 수정합니다. 종류를 바꾸면 해당 셀의 기존 기능층을 교체합니다. Open은 빈 공간으로 되돌립니다. Ctrl+Z 지원. 큰 브러시·드래그 칠하기는 없습니다.", MessageType.None);
        }

        private void Paint(ExcelTilemapSource source, Vector3Int cell)
        {
            try
            {
                //종류 순서는 DrawEdit의 Popup 항목 순서와 같음
                var kinds = new[] { MapCellKind.Solid, MapCellKind.OneWay, MapCellKind.Damage, MapCellKind.Open };
                ExcelMapSync.Paint(source, cell, source.regions[regionIndex].id, kinds[kindIndex]);
                editCell = new Vector2Int(cell.x, cell.y);
                status = "1셀 수정 완료. Unity → Excel 미리보기에서 반영할 셀을 확인하세요.";
                statusType = MessageType.Info;
                preview = null;
            }
            catch (Exception error)
            {
                status = error.Message;
                statusType = MessageType.Error;
            }
            Repaint();
            SceneView.RepaintAll();
        }

        //Scene 뷰 클릭으로 1셀 수정. Alt를 누른 동안은 기본 카메라 조작을 그대로 사용
        private void OnSceneGUI(SceneView view)
        {
            if (!editOneCell || !showEdit || target == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var source = ExcelTilemapImporter.FindSource(target);
            if (source == null || source.regions.Count == 0) return;
            Event e = Event.current;
            if (e.alt) return;

            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane = new Plane(Vector3.forward, target.transform.position);
            if (!plane.Raycast(ray, out float distance)) return;
            var cell = target.WorldToCell(ray.GetPoint(distance));
            cell.z = 0;

            //클릭이 다른 오브젝트 선택으로 넘어가지 않도록 기본 컨트롤을 이 창이 가져감
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Handles.color = Color.cyan;
            Handles.DrawWireCube(target.GetCellCenterWorld(cell), new Vector3(source.cellSize, source.cellSize, .01f));
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Paint(source, cell);
                e.Use();
            }
        }

        private bool DrawSettings(ExcelTilemapSource source)
        {
            var serialized = new SerializedObject(source);
            serialized.Update();
            EditorGUILayout.BeginHorizontal();
            var path = serialized.FindProperty("workbookPath");
            string requestedPath = EditorGUILayout.DelayedTextField(new GUIContent("XLSX 파일", "프로젝트 안은 상대 경로, 밖은 절대 경로로 저장합니다."), path.stringValue);
            if (GUILayout.Button("찾기", GUILayout.Width(48)))
            {
                string chosen = EditorUtility.OpenFilePanel("지도 Excel 선택", "", "xlsx");
                if (!string.IsNullOrEmpty(chosen)) requestedPath = chosen;
            }
            EditorGUILayout.EndHorizontal();
            if (requestedPath != path.stringValue)
            {
                try
                {
                    string storedPath = ExcelMapSync.StoreWorkbookPath(requestedPath);
                    string baseline = ExcelMapSync.PrepareWorkbookPathChange(source, storedPath);
                    //경로와 기준을 하나의 SerializedObject 변경으로 묶어 Undo 시에도 함께 되돌림
                    path.stringValue = storedPath;
                    serialized.FindProperty("syncBaseline").stringValue = baseline;
                }
                catch (Exception error)
                {
                    status = error.Message;
                    statusType = MessageType.Error;
                }
            }
            EditorGUILayout.PropertyField(serialized.FindProperty("sheetName"), new GUIContent("지도 시트"));
            EditorGUILayout.PropertyField(serialized.FindProperty("paletteSheetName"), new GUIContent("색상코드 시트"));
            EditorGUILayout.PropertyField(serialized.FindProperty("cellRange"), new GUIContent("지도 범위"));
            EditorGUILayout.PropertyField(serialized.FindProperty("excludedRanges"), new GUIContent("제외 범위"));
            EditorGUILayout.PropertyField(serialized.FindProperty("cellSize"), new GUIContent("1셀 크기 (U)"));
            EditorGUILayout.PropertyField(serialized.FindProperty("worldOrigin"), new GUIContent("월드 원점"));
            return serialized.ApplyModifiedProperties();
        }
    }

    internal static class ExcelTilemapImporter
    {
        private const string UndoName = "Excel 지역별 지도 가져오기";
        private const string GeneratedFolder = "Assets/ExcelTilemapImport/Generated";

        private sealed class RegionTiles
        {
            public readonly List<TileChangeData> Solid = new List<TileChangeData>();
            public readonly List<TileChangeData> OneWay = new List<TileChangeData>();
            public readonly List<TileChangeData> Damage = new List<TileChangeData>();
        }

        public static ExcelTilemapSource FindSource(Grid grid)
        {
            if (grid == null) return null;
            var sources = grid.GetComponentsInChildren<ExcelTilemapSource>(true);
            return sources.Length == 1 ? sources[0] : null;
        }

        public static Grid CreateTarget()
        {
            RequireEditMode();
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("프리팹 편집 모드를 닫고 씬에서 만드세요.");
            var root = new GameObject("Excel Map Grid", typeof(Grid), typeof(ExcelTilemapSource));
            var grid = root.GetComponent<Grid>();
            grid.cellSize = new Vector3(.25f, .25f, 1);
            Undo.RegisterCreatedObjectUndo(root, "Excel 지도 Grid 만들기");
            Selection.activeGameObject = root;
            return grid;
        }

        public static (XlsxMapData Map, ExcelTilemapSource Source) Import(ExcelTilemapSource source)
        {
            var plan = ExcelMapSync.Preview(source, false);
            ExcelMapSync.ApplyImport(source, plan);
            return (plan.Result, source);
        }

        internal static ExcelTilemapSource ApplyData(ExcelTilemapSource source, XlsxMapData map, string baseline, Action beforeApply = null)
        {
            RequireEditMode();
            var grid = ValidateTarget(source);
            var tiles = BuildTiles(map, GetSolidTile(source.cellSize), GetOneWayTile(source.cellSize));

            //색상표에서 사라진 기존 지역이 있으면 지역을 지우지 않고 중단
            if (source.regions != null)
                foreach (var region in source.regions)
                    if (!tiles.ContainsKey(region.id)) throw new FormatException("기존 지역이 색상표에 없습니다.");
            beforeApply?.Invoke();
            return Apply(source, grid, map, tiles, baseline);
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play Mode에서는 가져올 수 없습니다.");
        }

        //공통 Grid 하나, 지역 루트·층 참조, 변형 없는 배치를 검사. 손상됐으면 추측해서 고치지 않고 중단
        internal static Grid ValidateTarget(ExcelTilemapSource source)
        {
            if (source == null || EditorUtility.IsPersistent(source) || !source.gameObject.scene.IsValid() || PrefabStageUtility.GetPrefabStage(source.gameObject) != null)
                throw new InvalidOperationException("씬의 지도 가져오기 설정을 선택하세요.");
            var grid = source.GetComponentInParent<Grid>(true);
            if (grid == null || grid.transform.parent != null || FindSource(grid) != source)
                throw new InvalidOperationException("지도 루트에는 공통 Grid 하나와 가져오기 설정 하나가 필요합니다.");
            if (grid.transform.localScale != Vector3.one || grid.transform.localRotation != Quaternion.identity ||
                grid.cellLayout != GridLayout.CellLayout.Rectangle || grid.cellSwizzle != GridLayout.CellSwizzle.XYZ || grid.cellGap != Vector3.zero)
                throw new InvalidOperationException("Grid는 회전·배율·간격 없는 직사각형이어야 합니다.");
            if (!float.IsFinite(source.cellSize) || source.cellSize < .01f || source.cellSize > 100 || !float.IsFinite(source.worldOrigin.x) || !float.IsFinite(source.worldOrigin.y))
                throw new FormatException("셀 크기는 0.01~100U, 월드 원점은 유효한 숫자로 입력하세요.");

            var owned = new HashSet<Tilemap>();
            var legacySolid = source.GetComponent<Tilemap>();
            if (legacySolid != null)
            {
                if (source.regions != null && source.regions.Count != 0) throw new InvalidOperationException("이전 구조와 지역 구조가 섞여 있습니다.");
                ValidateLayers(grid.transform, legacySolid, source.oneWayTilemap, source.damageTilemap, owned, true);
            }
            else
            {
                if (source.gameObject != grid.gameObject) throw new InvalidOperationException("지역별 가져오기 설정은 공통 Grid에 두세요.");
                var ids = new HashSet<string>();
                var roots = new HashSet<Transform>();
                if (source.regions != null)
                    foreach (var region in source.regions)
                    {
                        if (region == null || string.IsNullOrEmpty(region.id) || !ids.Add(region.id) || region.root == null || !roots.Add(region.root) ||
                            region.root.parent != grid.transform || region.root.GetComponent<Grid>() != null)
                            throw new InvalidOperationException("지역 ID·루트 참조가 중복되거나 손상됐습니다. 이전 지도를 유지합니다.");
                        ValidateTransform(region.root);
                        ValidateLayers(region.root, region.solid, region.oneWay, region.damage, owned, false);
                    }
            }
            return grid;
        }

        private static void ValidateTransform(Transform value)
        {
            if (value.localPosition != Vector3.zero || value.localScale != Vector3.one || value.localRotation != Quaternion.identity)
                throw new InvalidOperationException($"{value.name}: 공통 셀 좌표를 유지하도록 지역·Tilemap의 위치/회전은 0, 배율은 1로 두세요.");
        }

        private static void ValidateLayers(Transform parent, Tilemap solid, Tilemap oneWay, Tilemap damage, HashSet<Tilemap> owned, bool legacy)
        {
            if (solid == null || oneWay == null || (!legacy && damage == null)) throw new InvalidOperationException("지역의 Tilemap 참조가 없습니다. 임의 재생성하지 않고 중단했습니다.");
            foreach (var layer in new[] { solid, oneWay, damage })
            {
                if (layer == null) continue;
                if (!owned.Add(layer) || layer.transform.parent != parent) throw new InvalidOperationException("Tilemap이 중복 지정되었거나 다른 지역으로 이동했습니다.");
                ValidateTransform(layer.transform);
            }
            if (solid.GetComponent<TilemapCollider2D>() == null) throw new InvalidOperationException("고체 Tilemap의 충돌체가 없습니다.");
            var body = oneWay.GetComponent<Rigidbody2D>();
            var collider = oneWay.GetComponent<TilemapCollider2D>();
            var composite = oneWay.GetComponent<CompositeCollider2D>();
            var effector = oneWay.GetComponent<PlatformEffector2D>();
            if (body == null || body.bodyType != RigidbodyType2D.Static || collider == null || collider.compositeOperation != Collider2D.CompositeOperation.Merge ||
                composite == null || !composite.usedByEffector || effector == null || !effector.useOneWay)
                throw new InvalidOperationException("일방향 Tilemap의 Collider·Static Rigidbody·PlatformEffector 설정을 확인하세요.");
            if (damage != null && (damage.GetComponent<Collider2D>() != null || damage.GetComponent<Rigidbody2D>() != null))
                throw new InvalidOperationException("빨간 표시는 Collider/Rigidbody 없이 두세요. 피해·복귀 동작은 미구현입니다.");
        }

        private static Dictionary<string, RegionTiles> BuildTiles(XlsxMapData map, Tile tile, Tile oneWayTile)
        {
            var result = new Dictionary<string, RegionTiles>();
            foreach (var region in map.Regions) result.Add(region.Id, new RegionTiles());
            for (int index = 0; index < map.Kinds.Length; index++)
            {
                var kind = map.Kinds[index];
                if (kind != MapCellKind.Solid && kind != MapCellKind.OneWay && kind != MapCellKind.Damage) continue;
                if (map.RegionIds[index] == null || !result.TryGetValue(map.RegionIds[index], out var region))
                    throw new FormatException("타일의 지역 귀속이 없습니다. 이전 지도를 유지합니다.");
                int rgb = map.Colors[index];

                //Excel 행은 아래로 갈수록 커지므로 Unity 셀 y로 뒤집음
                var position = new Vector3Int(index % map.Range.Width, map.Range.Height - 1 - index / map.Range.Width, 0);
                var change = new TileChangeData(position, kind == MapCellKind.OneWay ? oneWayTile : tile, new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255), Matrix4x4.identity);
                if (kind == MapCellKind.Solid) region.Solid.Add(change);
                else if (kind == MapCellKind.OneWay) region.OneWay.Add(change);
                else region.Damage.Add(change);
            }
            return result;
        }

        //지역 오브젝트는 그대로 유지하고 각 지역이 가진 타일만 교체. 수동으로 배치한 자식(기믹 등)은 건드리지 않음
        private static ExcelTilemapSource Apply(ExcelTilemapSource source, Grid grid, XlsxMapData map, Dictionary<string, RegionTiles> tiles, string baseline)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            var changed = new List<UnityEngine.Object> { source, grid, grid.transform };
            var legacySolid = source.GetComponent<Tilemap>();
            Tilemap[] templates = { legacySolid, source.oneWayTilemap, source.damageTilemap };
            if (legacySolid != null)
            {
                foreach (var layer in templates)
                    if (layer != null) changed.Add(layer);
            }
            else if (source.regions != null)
                foreach (var region in source.regions)
                {
                    changed.Add(region.solid);
                    changed.Add(region.oneWay);
                    changed.Add(region.damage);
                }
            Undo.RegisterCompleteObjectUndo(changed.ToArray(), UndoName);
            try
            {
                var previousSource = source;

                //셀에서 충돌체 형상을 다시 만들기 전에 공통 Grid 설정부터 맞춤
                grid.cellSize = new Vector3(source.cellSize, source.cellSize, 1);
                grid.transform.position = new Vector3(source.worldOrigin.x, source.worldOrigin.y, 0);
                if (legacySolid != null)
                {
                    //이전 구조의 설정을 공통 Grid로 옮기고, 기존 Tilemap 세 개를 그대로 재사용
                    source = Undo.AddComponent<ExcelTilemapSource>(grid.gameObject);
                }
                var importedRegions = source.regions == null ? new List<ExcelTilemapSource.RegionLayers>() : new List<ExcelTilemapSource.RegionLayers>(source.regions);
                var existing = new Dictionary<string, ExcelTilemapSource.RegionLayers>();
                foreach (var region in importedRegions) existing.Add(region.id, region);
                foreach (var definition in map.Regions)
                {
                    if (!existing.TryGetValue(definition.Id, out var region))
                    {
                        region = CreateRegion(grid, definition, templates, legacySolid != null && importedRegions.Count == 0);
                        importedRegions.Add(region);
                        existing.Add(region.id, region);
                    }
                    var data = tiles[definition.Id];

                    //새로 만든 Tilemap도 Redo를 위해 셀 데이터 기록이 필요
                    Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { region.solid, region.oneWay, region.damage }, UndoName);
                    ReplaceTiles(region.solid, data.Solid);
                    ReplaceTiles(region.oneWay, data.OneWay);
                    ReplaceTiles(region.damage, data.Damage);
                }

                //Redo가 완전한 목록을 받도록 모든 생성 작업이 끝난 뒤 지역 목록과 기준을 대입
                Undo.RecordObject(source, UndoName);
                if (legacySolid != null) CopySettings(previousSource, source);
                source.regions = importedRegions;
                source.syncBaseline = baseline;
                Undo.FlushUndoRecordObjects();
                if (legacySolid != null) Undo.DestroyObjectImmediate(previousSource);
                EditorUtility.SetDirty(source);
                Physics2D.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                return source;
            }
            catch
            {
                //중간에 실패하면 이번 가져오기 전체를 되돌려 부분 적용 상태를 남기지 않음
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static void CopySettings(ExcelTilemapSource from, ExcelTilemapSource to)
        {
            to.workbookPath = from.workbookPath;
            to.sheetName = from.sheetName;
            to.paletteSheetName = from.paletteSheetName;
            to.cellRange = from.cellRange;
            to.excludedRanges = from.excludedRanges;
            to.cellSize = from.cellSize;
            to.worldOrigin = from.worldOrigin;
        }

        private static ExcelTilemapSource.RegionLayers CreateRegion(Grid grid, MapRegion definition, Tilemap[] templates, bool reuseLegacy)
        {
            string label = string.IsNullOrEmpty(definition.Name) ? "" : " · " + definition.Name;
            var root = new GameObject("Region " + definition.Id + label);
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            root.transform.SetParent(grid.transform, false);
            var region = new ExcelTilemapSource.RegionLayers { id = definition.Id, root = root.transform };
            region.solid = reuseLegacy ? templates[0] : CreateLayer(root.transform, "Solid", false, true, templates[0]);
            region.oneWay = reuseLegacy ? templates[1] : CreateLayer(root.transform, "OneWay", true, true, templates[1]);
            region.damage = reuseLegacy && templates[2] != null ? templates[2] : CreateLayer(root.transform, "Damage Display", false, false, templates[2]);
            if (reuseLegacy)
                foreach (var layer in new[] { region.solid, region.oneWay, region.damage })
                    if (layer.transform.parent != root.transform) Undo.SetTransformParent(layer.transform, root.transform, UndoName);
            return region;
        }

        private static Tilemap CreateLayer(Transform parent, string name, bool oneWay, bool collision, Tilemap template)
        {
            var child = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            child.transform.SetParent(parent, false);
            if (collision) child.AddComponent<TilemapCollider2D>();
            if (oneWay)
            {
                child.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                var composite = child.AddComponent<CompositeCollider2D>();
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.usedByEffector = true;
                child.GetComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
                var effector = child.AddComponent<PlatformEffector2D>();
                effector.useColliderMask = false;
                effector.useOneWay = true;
                effector.useOneWayGrouping = true;
                effector.surfaceArc = 180;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
            }
            var map = child.GetComponent<Tilemap>();

            //같은 종류의 기존 층이 있으면 렌더링·레이어·표시 상태를 그대로 따라감
            if (template != null)
            {
                var original = template.GetComponent<TilemapRenderer>();
                var renderer = child.GetComponent<TilemapRenderer>();
                renderer.sharedMaterials = original.sharedMaterials;
                renderer.sortingLayerID = original.sortingLayerID;
                renderer.sortingOrder = original.sortingOrder;
                renderer.mode = original.mode;
                renderer.sortOrder = original.sortOrder;
                renderer.maskInteraction = original.maskInteraction;
                renderer.enabled = original.enabled;
                map.color = template.color;
                map.tileAnchor = template.tileAnchor;
                child.layer = template.gameObject.layer;
                child.tag = template.gameObject.tag;
                child.SetActive(template.gameObject.activeSelf);
                if (SceneVisibilityManager.instance.IsHidden(template.gameObject)) SceneVisibilityManager.instance.Hide(child, false);
                if (SceneVisibilityManager.instance.IsPickingDisabled(template.gameObject)) SceneVisibilityManager.instance.DisablePicking(child, false);
            }
            Undo.RegisterCreatedObjectUndo(child, UndoName);
            return map;
        }

        private static void ReplaceTiles(Tilemap tilemap, List<TileChangeData> changes)
        {
            tilemap.ClearAllTiles();
            tilemap.SetTiles(changes.ToArray(), true);
            tilemap.CompressBounds();
            var collider = tilemap.GetComponent<TilemapCollider2D>();
            if (collider != null) collider.ProcessTilemapChanges();
        }

        /// <summary> 착지 충돌은 한 셀을 유지하고, 그림은 위쪽 절반만 보여 주는 전용 일방향 타일 </summary>
        internal static Tile GetOneWayTile(float cellSize)
        {
            string size = cellSize.ToString("G9", CultureInfo.InvariantCulture).Replace('.', '_');
            string path = $"{GeneratedFolder}/OneWay_{size}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets/ExcelTilemapImport", "Generated");

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "OneWay top half", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(new[] { Color.clear, Color.clear, Color.white, Color.white });
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2f / cellSize, 0, SpriteMeshType.FullRect);
            sprite.name = "OneWay top-half sprite";
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = "Excel one-way top half";
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Grid;
            tile.flags = TileFlags.None;
            AssetDatabase.CreateAsset(tile, path);
            AssetDatabase.AddObjectToAsset(texture, tile);
            AssetDatabase.AddObjectToAsset(sprite, tile);
            AssetDatabase.SaveAssetIfDirty(tile);
            return tile;
        }

        internal static Tile GetSolidTile(float cellSize)
        {
            string size = cellSize.ToString("G9", CultureInfo.InvariantCulture).Replace('.', '_');
            string path = $"{GeneratedFolder}/Solid_{size}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets/ExcelTilemapImport", "Generated");

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "White cell", filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f / cellSize, 0, SpriteMeshType.FullRect);
            sprite.name = "Cell sprite";
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = "Excel solid cell";
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Grid;
            tile.flags = TileFlags.None;
            AssetDatabase.CreateAsset(tile, path);
            AssetDatabase.AddObjectToAsset(texture, tile);
            AssetDatabase.AddObjectToAsset(sprite, tile);
            AssetDatabase.SaveAssetIfDirty(tile);
            return tile;
        }
    }
}
