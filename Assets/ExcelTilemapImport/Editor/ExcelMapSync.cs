using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hwanjo.MapTools.Editor
{
    internal sealed class MapCellChange
    {
        public int Index;
        public string Address, Relation, Before, Excel, Unity;
        public bool Apply, Conflict;
    }

    internal sealed class MapSyncPlan
    {
        public bool Export, Initial;
        public string Signature, BaselineToken, ExcelHash, UnityToken;
        public XlsxMapData Baseline, Excel, Unity, Result, NextBaseline;
        public readonly List<MapCellChange> Changes = new List<MapCellChange>();
        public bool HasConflicts => Changes.Any(c => c.Conflict);
        public int ApplyCount => Changes.Count(c => c.Apply);
        public string Summary => (Initial ? $"새 지도 {Result.Kinds.Count(ExcelMapSync.Physical):N0}타일 · " : "") +
            $"{(Export ? "Unity → Excel" : "Excel → Unity")}: 적용 {ApplyCount:N0}셀 · " +
            $"충돌 {Changes.Count(c => c.Conflict):N0}셀 · 반대편 단독 변경 {Changes.Count(c => !c.Apply && !c.Conflict && c.Relation != "동일"):N0}셀 보존";
    }

    //공통 기준 셀 상태 하나와 기준·Excel·Unity 3자 비교로 동기화. 파일 자동 감시는 하지 않음
    internal static class ExcelMapSync
    {
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        internal static string ResolveWorkbookPath(string path) => ExcelWorkbookPath.Resolve(path, ProjectRoot);
        internal static string StoreWorkbookPath(string path) => ExcelWorkbookPath.Store(path, ProjectRoot);

        //프로젝트 안의 원본은 상대 경로로 식별해 체크아웃 위치가 바뀌어도 같은 기준을 사용
        internal static string Signature(ExcelTilemapSource s) => SignatureForPath(s, s.workbookPath);
        private static string SignatureForPath(ExcelTilemapSource s, string path) =>
            ExcelWorkbookPath.Key(path, ProjectRoot) + SettingsSignature(s);
        private static string SettingsSignature(ExcelTilemapSource s) =>
            "|" + s.sheetName + "|" + s.paletteSheetName + "|" +
            s.cellRange + "|" + s.excludedRanges + "|" + s.cellSize.ToString("R", CultureInfo.InvariantCulture) + "|" +
            s.worldOrigin.x.ToString("R", CultureInfo.InvariantCulture) + "|" + s.worldOrigin.y.ToString("R", CultureInfo.InvariantCulture);

        internal static string PrepareWorkbookPathChange(ExcelTilemapSource source, string nextPath)
        {
            if (string.IsNullOrEmpty(source.syncBaseline)) return source.syncBaseline;
            string previous = ExcelWorkbookPath.ReadSignature(source.syncBaseline);
            string legacy = ResolveWorkbookPath(source.workbookPath).ToUpperInvariant() + SettingsSignature(source);
            if (previous != Signature(source) && previous != legacy)
                throw new InvalidOperationException("기존 경로·설정과 기준이 일치하지 않아 이전을 중단했습니다. 기준을 지우지 않았습니다.");
            Unpack(source.syncBaseline, previous);
            string next = SignatureForPath(source, nextPath);
            if (previous == next) return source.syncBaseline;

            //현재 Excel이 기준보다 수정됐어도 허용하되, 이전·새 파일 자체는 동일해야 경로만 이전할 수 있음
            string beforeHash = XlsxMapReader.Hash(XlsxMapReader.ReadStableFile(ResolveWorkbookPath(source.workbookPath)));
            string afterHash = XlsxMapReader.Hash(XlsxMapReader.ReadStableFile(ResolveWorkbookPath(nextPath)));
            if (beforeHash != afterHash)
                throw new IOException("두 XLSX의 내용이 달라 경로를 이전하지 않았습니다. 기존 파일과 동일한 보존 사본을 선택하세요.");
            return ExcelWorkbookPath.Rebind(source.syncBaseline, previous, next);
        }

        internal static bool Physical(MapCellKind k) => k == MapCellKind.Solid || k == MapCellKind.OneWay || k == MapCellKind.Damage;
        internal static XlsxMapData ReadExcel(ExcelTilemapSource s) =>
            XlsxMapReader.Read(ResolveWorkbookPath(s.workbookPath), s.sheetName, s.cellRange, s.excludedRanges, s.paletteSheetName);
        internal static string Address(XlsxMapData m, int i) => XlsxMapReader.CellAddress(m.Range.Left + i % m.Range.Width, m.Range.Top + i / m.Range.Width);
        internal static Vector3Int Position(XlsxMapData m, int i) => new Vector3Int(i % m.Range.Width, m.Range.Height - 1 - i / m.Range.Width, 0);
        internal static bool Same(XlsxMapData a, XlsxMapData b, int i) => a.Kinds[i] == b.Kinds[i] && a.Colors[i] == b.Colors[i] && a.RegionIds[i] == b.RegionIds[i];

        internal static void CopyCell(XlsxMapData from, XlsxMapData to, int i)
        {
            to.Kinds[i] = from.Kinds[i];
            to.Colors[i] = from.Colors[i];
            to.RegionIds[i] = from.RegionIds[i];
        }

        internal static string Describe(XlsxMapData m, int i) => $"지역 {m.RegionIds[i] ?? "—"} / {m.Kinds[i]} / #{m.Colors[i]:X6}";

        internal static XlsxMapData Clone(XlsxMapData m) => new XlsxMapData
        {
            Range = m.Range, Kinds = (MapCellKind[])m.Kinds.Clone(), Colors = (int[])m.Colors.Clone(), RegionIds = (string[])m.RegionIds.Clone(),
            Regions = m.Regions, SourceStamp = m.SourceStamp, SourceHash = m.SourceHash
        };

        internal static MapSyncPlan Preview(ExcelTilemapSource source, bool export)
        {
            RequireEdit();
            ExcelTilemapImporter.ValidateTarget(source);
            var excel = ReadExcel(source);
            bool empty = source.regions == null || source.regions.Count == 0;
            if (empty && source.GetComponentsInChildren<Tilemap>(true).Any(t => t.GetUsedTilesCount() > 0))
                throw new InvalidOperationException("기준 없는 이전 구조에 타일이 있습니다. 알려진 snapshot과 일치를 먼저 검증해야 하므로 덮어쓰지 않았습니다.");

            //기준이 없으면 첫 가져오기만 허용. 이미 타일이 있다면 Excel과 완전히 같을 때만 그 상태를 기준으로 삼음
            XlsxMapData baseline;
            if (string.IsNullOrEmpty(source.syncBaseline))
            {
                if (export) throw new InvalidOperationException("기준 지도 없음: 먼저 Excel → Unity 미리보기로 기준을 확인하세요.");
                if (!empty)
                {
                    var current = ReadUnity(source, excel);
                    if (Enumerable.Range(0, excel.Kinds.Length).Any(i => !Same(excel, current, i)))
                        throw new InvalidOperationException("기준 없는 기존 지도와 Excel이 다릅니다. 신뢰할 수 있는 이전 snapshot으로 기준을 먼저 복구해야 합니다. 덮어쓰지 않았습니다.");
                }
                baseline = Clone(excel);
            }
            else baseline = Unpack(source.syncBaseline, Signature(source));

            var unity = empty ? Clone(baseline) : ReadUnity(source, baseline);
            var plan = Compare(source, baseline, excel, unity, export);
            plan.Initial = empty;
            return plan;
        }

        //기준 대비 Excel·Unity 변경을 셀마다 비교. 선택 방향의 단독 변경만 적용하고, 반대편 단독 변경은 보존, 양쪽이 다르게 바꾸면 충돌
        internal static MapSyncPlan Compare(ExcelTilemapSource source, XlsxMapData baseline, XlsxMapData excel, XlsxMapData unity, bool export)
        {
            if (baseline.Kinds.Length != excel.Kinds.Length || baseline.Kinds.Length != unity.Kinds.Length)
                throw new InvalidOperationException("기준과 지도 범위가 다릅니다. 범위를 임의로 재해석하지 않습니다.");

            var p = new MapSyncPlan
            {
                Export = export, Signature = Signature(source), BaselineToken = source.syncBaseline,
                Baseline = baseline, Excel = excel, Unity = unity, ExcelHash = excel.SourceHash,
                UnityToken = Pack(unity, Signature(source)), Result = Clone(export ? excel : unity), NextBaseline = Clone(baseline)
            };
            p.Result.Regions = excel.Regions;
            p.NextBaseline.Regions = excel.Regions;
            for (int i = 0; i < baseline.Kinds.Length; i++)
            {
                bool ec = !Same(baseline, excel, i), uc = !Same(baseline, unity, i);
                if (!ec && !uc) continue;

                bool same = Same(excel, unity, i), conflict = ec && uc && !same;
                bool apply = !conflict && !same && (export ? uc : ec);
                p.Changes.Add(new MapCellChange
                {
                    Index = i, Address = Address(baseline, i), Relation = conflict ? "충돌" : same ? "동일" : ec ? "Excel" : "Unity",
                    Before = Describe(baseline, i), Excel = Describe(excel, i), Unity = Describe(unity, i), Apply = apply, Conflict = conflict
                });
                if (apply) CopyCell(export ? unity : excel, p.Result, i);
                if (apply || same) CopyCell(p.Result, p.NextBaseline, i);
            }
            return p;
        }

        //미리보기 이후 설정·기준·Excel·Unity 중 하나라도 바뀌었으면 적용하지 않음
        private static void CheckFresh(ExcelTilemapSource source, MapSyncPlan p)
        {
            RequireEdit();
            if (p == null || Signature(source) != p.Signature || source.syncBaseline != p.BaselineToken)
                throw new InvalidOperationException("설정 또는 기준이 변경됐습니다. 미리보기를 다시 만드세요.");
            if (p.HasConflicts) throw new InvalidOperationException("양쪽에서 다르게 바꾼 셀이 있습니다. Excel 또는 Unity에서 원하는 값으로 맞춘 뒤 다시 미리보세요. 자동 덮어쓰지 않습니다.");
            var fresh = ReadExcel(source);
            if (fresh.SourceHash != p.ExcelHash) throw new IOException("미리보기 이후 Excel이 변경됐습니다. 다시 비교하세요.");
            if (p.Initial && ((source.regions != null && source.regions.Count != 0) || source.GetComponentsInChildren<Tilemap>(true).Any(t => t.GetUsedTilesCount() > 0)))
                throw new InvalidOperationException("새 지도의 미리보기 이후 타일/지역이 생겼습니다. 다시 비교하세요.");
            if (!p.Initial && Pack(ReadUnity(source, p.Baseline), Signature(source)) != p.UnityToken)
                throw new InvalidOperationException("미리보기 이후 Unity 타일이 변경됐습니다. 다시 비교하세요.");
        }

        internal static void ApplyImport(ExcelTilemapSource source, MapSyncPlan p)
        {
            if (p.Export) throw new InvalidOperationException("가져오기 미리보기가 아닙니다.");
            CheckFresh(source, p);
            ExcelTilemapImporter.ApplyData(source, p.Result, Pack(p.NextBaseline, p.Signature), () =>
            {
                if (XlsxMapReader.Hash(XlsxMapReader.ReadStableFile(ResolveWorkbookPath(source.workbookPath))) != p.ExcelHash)
                    throw new IOException("Unity 적용 직전 Excel이 변경됐습니다. 이전 Tilemap을 유지했습니다.");
            });
        }

        internal static string ApplyExport(ExcelTilemapSource source, MapSyncPlan p)
        {
            if (!p.Export) throw new InvalidOperationException("역반영 미리보기가 아닙니다.");
            CheckFresh(source, p);
            string backup = p.ApplyCount == 0 ? "" : XlsxMapWriter.Apply(source, p);

            //외부 Excel 파일 변경은 Unity Undo로 되돌릴 수 없음. 대신 원본 파일 백업을 남김
            source.syncBaseline = Pack(p.NextBaseline, p.Signature);
            EditorUtility.SetDirty(source);
            EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
            return backup;
        }

        //알려진 이전 통합문서가 현재 Unity 타일 전체와 일치함을 확인한 경우에만 사용 (현재 호출하는 곳 없음)
        internal static void SeedVerifiedBaseline(ExcelTilemapSource source, XlsxMapData snapshot)
        {
            var actual = ReadUnity(source, snapshot);
            if (Enumerable.Range(0, snapshot.Kinds.Length).Any(i => !Same(snapshot, actual, i)))
                throw new InvalidOperationException("기준 snapshot과 현재 Unity가 다릅니다. 기준을 만들지 않았습니다.");
            source.syncBaseline = Pack(snapshot, Signature(source));
        }

        //기준의 물리 셀을 먼저 지역 배경(Open)으로 비우고, 현재 Tilemap의 타일을 읽어 다시 채움
        //알 수 없는 타일·변형·지역 밖 타일은 추측하지 않고 중단
        internal static XlsxMapData ReadUnity(ExcelTilemapSource source, XlsxMapData baseline)
        {
            var grid = ExcelTilemapImporter.ValidateTarget(source);
            if (grid.cellSize != new Vector3(source.cellSize, source.cellSize, 1) || grid.transform.position != new Vector3(source.worldOrigin.x, source.worldOrigin.y, 0))
                throw new InvalidOperationException("Grid의 실제 셀 크기/위치가 설정과 다릅니다.");
            var known = AssetDatabase.LoadAssetAtPath<Tile>($"Assets/ExcelTilemapImport/Generated/Solid_{source.cellSize.ToString("G9",CultureInfo.InvariantCulture).Replace('.','_')}.asset");
            var knownOneWay = AssetDatabase.LoadAssetAtPath<Tile>($"Assets/ExcelTilemapImport/Generated/OneWay_{source.cellSize.ToString("G9",CultureInfo.InvariantCulture).Replace('.','_')}.asset");
            var result = Clone(baseline);
            var regionById = baseline.Regions.ToDictionary(r => r.Id);
            for (int i = 0; i < result.Kinds.Length; i++)
                if (Physical(result.Kinds[i]))
                {
                    if (result.RegionIds[i] == null || !regionById.TryGetValue(result.RegionIds[i], out var r)) throw new InvalidOperationException("기준의 지역 귀속이 없습니다.");
                    result.Kinds[i] = MapCellKind.Open;
                    result.Colors[i] = r.OpenColor;
                }

            var occupied = new HashSet<int>();
            foreach (var r in source.regions)
            {
                if (!regionById.TryGetValue(r.id, out var definition)) throw new InvalidOperationException($"알 수 없는 지역 {r.id}");
                var layers = new[] { r.solid, r.oneWay, r.damage };
                var kinds = new[] { MapCellKind.Solid, MapCellKind.OneWay, MapCellKind.Damage };
                for (int k = 0; k < layers.Length; k++)
                {
                    var layer = layers[k];
                    if (layer.color != Color.white || layer.tileAnchor != new Vector3(.5f, .5f, 0) || layer.orientation != Tilemap.Orientation.XY || layer.orientationMatrix != Matrix4x4.identity)
                        throw new InvalidOperationException($"{r.id}/{layer.name}: Tilemap 색/앵커/방향 변형을 지원하지 않습니다.");
                    if ((long)layer.cellBounds.size.x * layer.cellBounds.size.y * layer.cellBounds.size.z > 1000000)
                        throw new InvalidOperationException("Tilemap의 셀 범위가 비정상적으로 큽니다.");
                    foreach (var cell in layer.cellBounds.allPositionsWithin)
                    {
                        var tile = layer.GetTile(cell);
                        if (tile == null) continue;
                        if (cell.z != 0 || cell.x < 0 || cell.x >= baseline.Range.Width || cell.y < 0 || cell.y >= baseline.Range.Height)
                            throw new InvalidOperationException($"{r.id}/{layer.name} {cell}: 지도 밖 타일입니다.");

                        //Unity 셀 y는 위쪽이 크고 Excel 행은 아래쪽이 크므로 뒤집어 인덱스를 계산
                        int i = (baseline.Range.Height - 1 - cell.y) * baseline.Range.Width + cell.x;
                        if (!occupied.Add(i)) throw new InvalidOperationException($"{Address(baseline,i)}: 지역/기능층에 타일이 겹칩니다.");
                        if (baseline.RegionIds[i] != r.id || baseline.Kinds[i] == MapCellKind.Excluded || baseline.Kinds[i] == MapCellKind.Marker || baseline.Kinds[i] == MapCellKind.Outside)
                            throw new InvalidOperationException($"{Address(baseline,i)}: 지역 밖·미할당·제외·표식 셀에는 임의로 타일을 둘 수 없습니다.");

                        //이전 씬의 공통 타일도 일방향 층에서 읽을 수 있지만, 새로 칠하거나 가져올 때는 전용 타일을 사용
                        bool knownTile = tile == known || (k == 1 && knownOneWay != null && tile == knownOneWay);
                        if (!knownTile || layer.GetTransformMatrix(cell) != Matrix4x4.identity)
                            throw new InvalidOperationException($"{Address(baseline,i)}: 알 수 없는 타일 또는 셀 변형입니다.");
                        int color = k == 0 ? definition.SolidColor : k == 1 ? 0xFF8000 : 0xFF0000;
                        Color32 c = layer.GetColor(cell);
                        if (c.a != 255 || ((c.r << 16) | (c.g << 8) | c.b) != color)
                            throw new InvalidOperationException($"{Address(baseline,i)}: 지역/기능색 불일치입니다. 이 창의 1셀 수정으로 지역과 종류를 선택하세요.");
                        result.Kinds[i] = kinds[k];
                        result.Colors[i] = color;
                        result.RegionIds[i] = r.id;
                    }
                }
            }
            return result;
        }

        //선택 지역의 세 기능층에서 해당 셀을 지운 뒤 고른 종류의 층에만 타일을 칠함 (Undo 지원, 기준은 바꾸지 않음)
        internal static void Paint(ExcelTilemapSource source, Vector3Int cell, string regionId, MapCellKind kind)
        {
            RequireEdit();
            ExcelTilemapImporter.ValidateTarget(source);
            var baseline = Unpack(source.syncBaseline, Signature(source));
            if (cell.z != 0 || cell.x < 0 || cell.x >= baseline.Range.Width || cell.y < 0 || cell.y >= baseline.Range.Height) throw new InvalidOperationException("지도 밖 셀입니다.");
            int i = (baseline.Range.Height - 1 - cell.y) * baseline.Range.Width + cell.x;
            if (baseline.RegionIds[i] != regionId || (!Physical(baseline.Kinds[i]) && baseline.Kinds[i] != MapCellKind.Open))
                throw new InvalidOperationException("선택 지역 밖·미할당·표식·제외 셀은 수정하지 않습니다.");
            if (kind != MapCellKind.Open && !Physical(kind)) throw new InvalidOperationException("지원하지 않는 종류입니다.");

            var region = source.regions.Single(r => r.id == regionId);
            var definition = baseline.Regions.Single(r => r.Id == regionId);
            var maps = new[] { region.solid, region.oneWay, region.damage };
            Undo.RegisterCompleteObjectUndo(maps, "Excel 지도 1셀 수정");
            foreach (var map in maps) map.SetTile(cell, null);
            if (kind != MapCellKind.Open)
            {
                int k = kind == MapCellKind.Solid ? 0 : kind == MapCellKind.OneWay ? 1 : 2;
                int rgb = k == 0 ? definition.SolidColor : k == 1 ? 0xFF8000 : 0xFF0000;
                maps[k].SetTiles(new[] { new TileChangeData(cell, k == 1 ? ExcelTilemapImporter.GetOneWayTile(source.cellSize) : ExcelTilemapImporter.GetSolidTile(source.cellSize), new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255), Matrix4x4.identity) }, true);
            }
            foreach (var map in maps)
            {
                var collider = map.GetComponent<TilemapCollider2D>();
                if (collider != null) collider.ProcessTilemapChanges();
                EditorUtility.SetDirty(map);
            }
            EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
        }

        private static void RequireEdit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode에서는 지도 동기화/수정이 불가능합니다.");
        }

        //기준 지도를 씬에 저장하기 위한 이진 직렬화 → Deflate 압축 → Base64. 첫 값은 형식 버전(1)
        internal static string Pack(XlsxMapData m, string signature)
        {
            using var output = new MemoryStream();
            using (var zip = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
            using (var writer = new BinaryWriter(zip, Encoding.UTF8, true))
            {
                writer.Write(1);
                writer.Write(signature);
                writer.Write(Address(m, 0) + ":" + Address(m, m.Kinds.Length - 1));
                writer.Write(m.Regions.Length);
                foreach (var r in m.Regions)
                {
                    writer.Write(r.Id);
                    writer.Write(r.Name ?? "");
                    writer.Write(r.OpenColor);
                    writer.Write(r.SolidColor);
                    writer.Write(r.Left);
                    writer.Write(r.Top);
                    writer.Write(r.Right);
                    writer.Write(r.Bottom);
                }

                //셀의 지역은 지역 목록 순번+1로 저장 (0은 지역 없음)
                var ids = m.Regions.Select((r, i) => (r.Id, i)).ToDictionary(v => v.Id, v => v.i + 1);
                writer.Write(m.Kinds.Length);
                for (int i = 0; i < m.Kinds.Length; i++)
                {
                    writer.Write((byte)m.Kinds[i]);
                    writer.Write(m.Colors[i]);
                    writer.Write((byte)(m.RegionIds[i] == null ? 0 : ids[m.RegionIds[i]]));
                }
            }
            return Convert.ToBase64String(output.ToArray());
        }

        //Pack의 역순. 설정 서명이 다르거나 값이 범위를 벗어나면 기준을 임의로 버리지 않고 중단
        internal static XlsxMapData Unpack(string packed, string signature)
        {
            if (string.IsNullOrEmpty(packed) || packed.Length > 20000000) throw new InvalidOperationException("유효한 기준 지도가 없습니다.");
            using var input = new MemoryStream(Convert.FromBase64String(packed));
            using var zip = new DeflateStream(input, CompressionMode.Decompress);
            using var reader = new BinaryReader(zip, Encoding.UTF8);
            if (reader.ReadInt32() != 1 || reader.ReadString() != signature) throw new InvalidOperationException("기준과 파일/시트/범위/제외/좌표 설정이 다릅니다. 기존 기준을 임의로 버리지 않습니다.");

            var map = new XlsxMapData { Range = CellRectangle.Parse(reader.ReadString()) };
            int count = reader.ReadInt32();
            if (count < 1 || count > 100) throw new InvalidDataException("기준의 지역 수 오류");
            map.Regions = new MapRegion[count];
            for (int i = 0; i < count; i++)
                map.Regions[i] = new MapRegion
                {
                    Id = reader.ReadString(), Name = reader.ReadString(), OpenColor = reader.ReadInt32(), SolidColor = reader.ReadInt32(),
                    Left = reader.ReadInt32(), Top = reader.ReadInt32(), Right = reader.ReadInt32(), Bottom = reader.ReadInt32()
                };

            int length = reader.ReadInt32();
            if (length < 1 || length > 500000 || length != (long)map.Range.Width * map.Range.Height) throw new InvalidDataException("기준의 셀 수 오류");
            map.Kinds = new MapCellKind[length];
            map.Colors = new int[length];
            map.RegionIds = new string[length];
            for (int i = 0; i < length; i++)
            {
                int kind = reader.ReadByte();
                if (kind > 6) throw new InvalidDataException("기준 셀 종류 오류");
                map.Kinds[i] = (MapCellKind)kind;
                map.Colors[i] = reader.ReadInt32();
                int owner = reader.ReadByte();
                if (owner > count) throw new InvalidDataException("기준 지역 참조 오류");
                map.RegionIds[i] = owner == 0 ? null : map.Regions[owner - 1].Id;
            }
            return map;
        }
    }
}
