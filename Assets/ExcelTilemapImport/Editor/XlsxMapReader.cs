using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Hwanjo.MapTools.Editor
{
    internal enum MapCellKind
    {
        Outside,
        Open,
        Solid,
        OneWay,
        Damage,
        Marker,
        Excluded
    }

    internal sealed class MapRegion
    {
        public string Id, Name;
        public int OpenColor, SolidColor;
        //지역 색이 칠해진 셀들의 Excel 행·열 범위. 셀이 없으면 HasCells가 false
        public int Left = int.MaxValue, Top = int.MaxValue, Right = int.MinValue, Bottom = int.MinValue;
        public bool HasCells => Left <= Right && Top <= Bottom;
        public bool Contains(int column, int row) => HasCells && column >= Left && column <= Right && row >= Top && row <= Bottom;
        public bool Overlaps(MapRegion other) => HasCells && other.HasCells && Left <= other.Right && Right >= other.Left && Top <= other.Bottom && Bottom >= other.Top;
    }

    internal sealed class XlsxMapData
    {
        public CellRectangle Range;
        public MapCellKind[] Kinds;
        public int[] Colors;
        public string[] RegionIds;
        public MapRegion[] Regions;
        public string SourceStamp, SourceHash;
        public readonly List<string> Markers = new List<string>();

        public string Summary()
        {
            int Count(MapCellKind kind) => Kinds.Count(value => value == kind);
            return $"{Range.Width} × {Range.Height}셀 | 지역 {Regions.Length}개 | 고체 {Count(MapCellKind.Solid):N0}, " +
                   $"일방향 발판 {Count(MapCellKind.OneWay):N0}, " +
                   $"피해 표시 {Count(MapCellKind.Damage):N0} (동작 미구현), " +
                   $"열린 공간 {Count(MapCellKind.Open):N0}, 미할당 {Count(MapCellKind.Outside):N0}, " +
                   $"표식 {Markers.Count:N0}, 제외 {Count(MapCellKind.Excluded):N0}";
        }
    }

    internal readonly struct CellRectangle
    {
        public readonly int Left, Top, Right, Bottom;
        public int Width => Right - Left + 1;
        public int Height => Bottom - Top + 1;

        private CellRectangle(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public bool Contains(int column, int row) =>
            column >= Left && column <= Right && row >= Top && row <= Bottom;

        public bool Overlaps(CellRectangle other) =>
            Left <= other.Right && Right >= other.Left && Top <= other.Bottom && Bottom >= other.Top;

        public static CellRectangle Parse(string text)
        {
            string[] ends = (text ?? "").Trim().Split(':');
            if (ends.Length < 1 || ends.Length > 2) throw new FormatException("셀 범위 예: F15:ST373");
            var first = Address(ends[0]);
            var last = Address(ends[ends.Length - 1]);
            if (first.column > last.column || first.row > last.row)
                throw new FormatException("셀 범위는 왼쪽 위:오른쪽 아래 순서입니다.");
            return new CellRectangle(first.column, first.row, last.column, last.row);
        }

        public static (int column, int row) Address(string text)
        {
            var match = Regex.Match(text.Trim(), @"^\$?([A-Za-z]{1,3})\$?([1-9][0-9]{0,6})$");
            if (!match.Success) throw new FormatException($"잘못된 셀 주소: {text}");
            int column = 0;
            foreach (char c in match.Groups[1].Value.ToUpperInvariant())
                column = column * 26 + c - 'A' + 1;
            int row = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (column > 16384 || row > 1048576) throw new FormatException($"Excel 범위 밖: {text}");
            return (column, row);
        }
    }

    internal static class XlsxMapReader
    {
        private readonly struct PaletteEntry
        {
            public readonly MapCellKind Kind;
            public readonly string RegionId;
            public PaletteEntry(MapCellKind kind, string regionId)
            {
                Kind = kind;
                RegionId = regionId;
            }
        }

        private static readonly XNamespace SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace DocumentRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const long MaxXmlBytes = 64 * 1024 * 1024;
        private const int MaxCells = 500000;

        private static readonly string[] RegionIds =
        {
            "1", "2", "3", "3-1", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "14-1", "15", "16", "17"
        };

        //지형으로 만들지 않는 기획 표식 색. 가져오기 결과 요약에만 위치를 남김
        private static readonly Dictionary<int, string> MarkerNames = new Dictionary<int, string>
        {
            { 0xD7A944, "위험 후보" }, { 0x4DCCBD, "조작 패드" }, { 0xD85C68, "문" },
            { 0xD998E0, "버튼" }, { 0xE6BD62, "접근 미정" }, { 0x74D8F0, "대시 획득" },
            { 0xE5BE74, "이전 계단 표식: 지형 제외, 별도 오브젝트 필요" }
        };

        public static XlsxMapData Read(string path, string sheetName, string rangeText, string exclusions, string paletteSheetName = "지역별 색상코드")
        {
            var range = CellRectangle.Parse(rangeText);
            if ((long)range.Width * range.Height > MaxCells) throw new FormatException("범위는 500,000셀 이하로 지정하세요.");
            var excluded = (exclusions ?? "").Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(CellRectangle.Parse).ToArray();
            string stamp = FileStamp(path);
            byte[] bytes = ReadStableFile(path);
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (archive.Entries.Sum(entry => entry.Length) > 256 * 1024 * 1024L)
                throw new InvalidDataException("압축 해제 크기가 너무 큰 XLSX입니다.");
            var palette = ReadPalette(archive, paletteSheetName, out var regions);
            var sheet = ReadXml(archive, FindSheetPath(archive, sheetName));
            ValidateFormatting(sheet, range, excluded);
            var colorsByStyle = ReadStyleColors(ReadXml(archive, "xl/styles.xml"));
            var map = ReadCells(sheet, range, excluded, colorsByStyle, palette, regions);
            AssignFunctionalRegions(map);
            //읽는 도중 파일이 바뀌었으면 섞인 결과를 쓰지 않도록 중단
            if (FileStamp(path) != stamp) throw new IOException("검사 중 Excel이 변경됐습니다. 저장 완료 후 다시 가져오세요.");
            map.SourceStamp = stamp;
            map.SourceHash = Hash(bytes);
            return map;
        }

        internal static string Hash(byte[] bytes)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        public static string FileStamp(string path)
        {
            var file = new FileInfo(path);
            return file.Exists ? $"{file.Length}:{file.LastWriteTimeUtc.Ticks}" : "missing";
        }

        private static Dictionary<int, PaletteEntry> ReadPalette(ZipArchive archive, string sheetName, out MapRegion[] regionInfo)
        {
            var sheet = ReadXml(archive, FindSheetPath(archive, sheetName));
            var sharedStrings = archive.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>() :
                ReadXml(archive, "xl/sharedStrings.xml").Root.Elements(SheetNs + "si")
                    .Select(item => string.Concat(item.Descendants(SheetNs + "t").Select(t => t.Value))).ToArray();
            var cells = sheet.Descendants(SheetNs + "c").ToDictionary(c => (string)c.Attribute("r"), c => CellText(c, sharedStrings));
            string Text(string address) => cells.TryGetValue(address, out string value) ? value.Trim() : "";
            if (Text("A5") != "구역" || Text("C5") != "배경 HEX" || Text("D5") != "땅 HEX")
                throw new FormatException($"{sheetName}: A5/C5/D5의 구역·배경 HEX·땅 HEX 표 형식을 확인하세요.");

            var palette = new Dictionary<int, PaletteEntry>();
            var regions = new HashSet<string>();
            var descriptions = new List<MapRegion>();
            for (int row = 6; row <= 24; row++)
            {
                string region = Text($"A{row}");
                if (!RegionIds.Contains(region) || !regions.Add(region))
                    throw new FormatException($"{sheetName}!A{row}: 19개 구역 번호가 없거나 중복됐습니다.");
                descriptions.Add(new MapRegion
                {
                    Id = region,
                    Name = Text($"B{row}"),
                    OpenColor = int.Parse(Text($"C{row}").TrimStart('#'), NumberStyles.HexNumber),
                    SolidColor = int.Parse(Text($"D{row}").TrimStart('#'), NumberStyles.HexNumber)
                });
                AddPaletteColor(palette, Text($"C{row}"), MapCellKind.Open, $"{sheetName}!C{row}", region);
                AddPaletteColor(palette, Text($"D{row}"), MapCellKind.Solid, $"{sheetName}!D{row}", region);
            }
            if (!Text("B28").Equals("#FF8000", StringComparison.OrdinalIgnoreCase) || Text("E28") != "oneway" ||
                !Text("B29").Equals("#FF0000", StringComparison.OrdinalIgnoreCase) || Text("E29") != "damage")
                throw new FormatException($"{sheetName}: 확정 기능색은 B28 #FF8000 / oneway, B29 #FF0000 / damage입니다.");
            AddPaletteColor(palette, Text("B28"), MapCellKind.OneWay, $"{sheetName}!B28");
            AddPaletteColor(palette, Text("B29"), MapCellKind.Damage, $"{sheetName}!B29");
            regionInfo = descriptions.ToArray();
            return palette;
        }

        private static void AddPaletteColor(Dictionary<int, PaletteEntry> palette, string hex, MapCellKind kind, string address, string regionId = null)
        {
            if (!Regex.IsMatch(hex, "^#[0-9A-Fa-f]{6}$")) throw new FormatException($"{address}: #RRGGBB 형식의 HEX가 필요합니다.");
            int color = int.Parse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (color == 0xFFFFFF || MarkerNames.ContainsKey(color) || !palette.TryAdd(color, new PaletteEntry(kind, regionId)))
                throw new FormatException($"{address}: 중복되거나 흰색·이전 표식과 겹치는 색입니다: {hex}");
        }

        private static string CellText(XElement cell, string[] sharedStrings)
        {
            if ((string)cell.Attribute("t") == "inlineStr")
                return string.Concat(cell.Descendants(SheetNs + "t").Select(t => t.Value));
            string value = cell.Element(SheetNs + "v")?.Value ?? "";
            if ((string)cell.Attribute("t") != "s") return value;
            if (!int.TryParse(value, out int index) || index < 0 || index >= sharedStrings.Length)
                throw new FormatException($"{cell.Attribute("r")?.Value}: 잘못된 Excel 문자열 참조입니다.");
            return sharedStrings[index];
        }

        internal static byte[] ReadStableFile(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new FormatException(".xlsx 파일만 지원합니다. 매크로나 Excel 프로그램을 실행하지 않습니다.");
            var before = new FileInfo(path);
            long length = before.Length;
            DateTime modified = before.LastWriteTimeUtc;
            if (length == 0 || length > 32 * 1024 * 1024) throw new IOException("XLSX 크기는 0 초과 32MB 이하여야 합니다.");
            //Excel이 쓰기 핸들을 열어 둘 수 있으므로, 저장을 막지 않는 공유 모드로 두 번 읽어 내용이 같은지 확인
            byte[] copy = ReadSnapshot(path);
            byte[] confirmation = ReadSnapshot(path);
            var after = new FileInfo(path);
            if (copy.Length != length || after.Length != length || after.LastWriteTimeUtc != modified || !copy.SequenceEqual(confirmation))
                throw new IOException("Excel 저장 중입니다. 저장 완료 후 다시 가져오세요.");
            return copy;
        }

        private static byte[] ReadSnapshot(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }

        internal static XDocument ReadXml(ZipArchive archive, string name)
        {
            var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"XLSX 항목 없음: {name}");
            if (entry.Length > MaxXmlBytes) throw new InvalidDataException($"XML 크기 초과: {name}");
            using var stream = entry.Open();
            //외부 DTD·엔터티를 읽지 않고 문서 크기를 제한해 손상되거나 악성인 XLSX를 막음
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxXmlBytes
            });
            return XDocument.Load(reader);
        }

        internal static string FindSheetPath(ZipArchive archive, string name)
        {
            var workbook = ReadXml(archive, "xl/workbook.xml");
            var sheet = workbook.Descendants(SheetNs + "sheet").SingleOrDefault(s => (string)s.Attribute("name") == name);
            if (sheet == null) throw new FormatException($"시트가 없습니다: {name}");
            string id = (string)sheet.Attribute(DocumentRelNs + "id");
            var rels = ReadXml(archive, "xl/_rels/workbook.xml.rels");
            var rel = rels.Root.Elements().SingleOrDefault(r => (string)r.Attribute("Id") == id);
            if (rel == null || (string)rel.Attribute("TargetMode") == "External")
                throw new InvalidDataException("외부 시트 연결은 지원하지 않습니다.");
            string target = (string)rel.Attribute("Target") ?? "";
            if (target.Contains("..") || target.Contains("\\") || target.Contains(":"))
                throw new InvalidDataException("올바르지 않은 XLSX 시트 경로입니다.");
            return target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
        }

        private static string[] ReadStyleColors(XDocument styles)
        {
            var fills = styles.Root.Element(SheetNs + "fills")?.Elements().Select(ReadFill).ToArray();
            var formats = styles.Root.Element(SheetNs + "cellXfs")?.Elements().ToArray();
            if (fills == null || formats == null) throw new InvalidDataException("셀 서식 정보가 없습니다.");
            return formats.Select(format =>
            {
                int index = (int?)format.Attribute("fillId") ?? 0;
                return index >= 0 && index < fills.Length ? fills[index] : "ERROR: 잘못된 fillId";
            }).ToArray();
        }

        private static string ReadFill(XElement fill)
        {
            var pattern = fill.Element(SheetNs + "patternFill");
            if (pattern == null) return "ERROR: 그라데이션 등 미지원 채우기";
            string type = (string)pattern.Attribute("patternType") ?? "none";
            if (type == "none") return "";
            if (type != "solid") return "ERROR: 단색 채우기만 지원";
            var foreground = pattern.Element(SheetNs + "fgColor");
            string rgb = (string)foreground?.Attribute("rgb");
            double tint = (double?)foreground?.Attribute("tint") ?? 0;
            if (tint != 0 || foreground?.Attribute("theme") != null || foreground?.Attribute("indexed") != null)
                return "ERROR: theme/indexed/tint 색은 명시적 RGB로 바꾸세요";
            if (rgb == null || !Regex.IsMatch(rgb, @"^(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$"))
                return "ERROR: 해석할 수 없는 채우기 색";
            //셀 채우기는 RGB로 해석. 저장된 알파 바이트는 투명 지형을 뜻하지 않으므로 무시
            return rgb.Substring(rgb.Length - 6).ToUpperInvariant();
        }

        private static void ValidateFormatting(XDocument sheet, CellRectangle range, CellRectangle[] excluded)
        {
            bool IsExcluded(CellRectangle rect) => excluded.Any(e => e.Left <= rect.Left && e.Right >= rect.Right && e.Top <= rect.Top && e.Bottom >= rect.Bottom);
            foreach (var merged in sheet.Descendants(SheetNs + "mergeCell"))
            {
                var rect = CellRectangle.Parse((string)merged.Attribute("ref"));
                if (rect.Overlaps(range) && !IsExcluded(rect))
                    throw new FormatException($"지형 범위의 병합 셀: {merged.Attribute("ref")?.Value}. 번호는 도형으로 넣거나 제외 범위를 지정하세요.");
            }
            foreach (var rule in sheet.Descendants(SheetNs + "conditionalFormatting"))
                foreach (string address in ((string)rule.Attribute("sqref") ?? "").Split(' '))
                {
                    if (string.IsNullOrWhiteSpace(address)) continue;
                    var rect = CellRectangle.Parse(address);
                    if (rect.Overlaps(range) && !IsExcluded(rect)) throw new FormatException("지형의 조건부 서식은 지원하지 않습니다. 셀 채우기를 사용하세요.");
                }
            //셀에 직접 칠한 채우기만 읽음. 행·열에서 상속된 서식은 잘못 읽지 않도록 거부
            if (sheet.Descendants(SheetNs + "col").Any(c => c.Attribute("style") != null &&
                    (int)c.Attribute("min") <= range.Right && (int)c.Attribute("max") >= range.Left) ||
                sheet.Descendants(SheetNs + "row").Any(r => r.Attribute("s") != null &&
                    (int)r.Attribute("r") >= range.Top && (int)r.Attribute("r") <= range.Bottom))
                throw new FormatException("행·열 전체 서식은 지원하지 않습니다. 선택한 셀에 직접 채우기를 적용하세요.");
        }

        private static XlsxMapData ReadCells(XDocument sheet, CellRectangle range, CellRectangle[] exclusions, string[] styles, Dictionary<int, PaletteEntry> palette, MapRegion[] regions)
        {
            int size = range.Width * range.Height;
            var map = new XlsxMapData
            {
                Range = range, Kinds = new MapCellKind[size], Colors = new int[size], RegionIds = new string[size], Regions = regions
            };
            var regionById = regions.ToDictionary(region => region.Id);
            for (int y = 0; y < range.Height; y++)
                for (int x = 0; x < range.Width; x++)
                    if (exclusions.Any(e => e.Contains(x + range.Left, y + range.Top)))
                        map.Kinds[y * range.Width + x] = MapCellKind.Excluded;

            var seen = new HashSet<int>();
            foreach (var cell in sheet.Descendants(SheetNs + "c"))
            {
                string address = (string)cell.Attribute("r") ?? throw new FormatException("주소 없는 셀입니다.");
                var position = CellRectangle.Address(address);
                if (!range.Contains(position.column, position.row)) continue;
                int x = position.column - range.Left, y = position.row - range.Top;
                int index = y * range.Width + x;
                if (map.Kinds[index] == MapCellKind.Excluded) continue;
                if (!seen.Add(index)) throw new FormatException($"중복 셀: {address}");
                int style = (int?)cell.Attribute("s") ?? 0;
                if (style < 0 || style >= styles.Length) throw new FormatException($"{address}: 잘못된 셀 서식");
                string fill = styles[style];
                if (fill.StartsWith("ERROR:")) throw new FormatException($"{address}: {fill}");
                if (fill == "" || fill == "FFFFFF") continue;
                int color = int.Parse(fill, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                map.Colors[index] = color;
                var entry = Classify(color, address, palette);
                map.Kinds[index] = entry.Kind;
                map.RegionIds[index] = entry.RegionId;
                if (entry.RegionId != null)
                {
                    var region = regionById[entry.RegionId];
                    region.Left = Math.Min(region.Left, position.column);
                    region.Right = Math.Max(region.Right, position.column);
                    region.Top = Math.Min(region.Top, position.row);
                    region.Bottom = Math.Max(region.Bottom, position.row);
                }
                if (map.Kinds[index] == MapCellKind.Marker)
                    map.Markers.Add($"{MarkerNames[color]} #{color:X6}: {address} → ({x}, {range.Height - 1 - y}, 0)");
            }
            return map;
        }

        private static PaletteEntry Classify(int color, string address, Dictionary<int, PaletteEntry> palette)
        {
            if (palette.TryGetValue(color, out var kind)) return kind;
            if (MarkerNames.ContainsKey(color)) return new PaletteEntry(MapCellKind.Marker, null);
            throw new FormatException($"{address}: 등록되지 않은 색 #{color:X6}. 전체 적용을 중단했습니다.");
        }

        private static void AssignFunctionalRegions(XlsxMapData map)
        {
            //지역 범위는 현재 지역별 땅·배경 색 셀에서만 계산 (이전 기획 메모는 사용하지 않음)
            //범위가 겹치면 기능색 셀의 지역을 추측하지 않고 중단
            for (int a = 0; a < map.Regions.Length; a++)
                for (int b = a + 1; b < map.Regions.Length; b++)
                    if (map.Regions[a].Overlaps(map.Regions[b]))
                        throw new FormatException($"지역 {map.Regions[a].Id}와 {map.Regions[b].Id}의 색상 범위가 겹칩니다. 지역 귀속을 추측하지 않고 적용을 중단했습니다.");
            for (int index = 0; index < map.Kinds.Length; index++)
            {
                if (map.Kinds[index] != MapCellKind.OneWay && map.Kinds[index] != MapCellKind.Damage) continue;
                int column = map.Range.Left + index % map.Range.Width;
                int row = map.Range.Top + index / map.Range.Width;
                var owners = map.Regions.Where(region => region.Contains(column, row)).ToArray();
                if (owners.Length != 1)
                    throw new FormatException($"{CellAddress(column, row)}: 기능색 #{map.Colors[index]:X6}의 지역을 하나로 결정할 수 없습니다. 지역 배경·땅 색의 범위를 확인하세요.");
                map.RegionIds[index] = owners[0].Id;
            }
        }

        internal static string CellAddress(int column, int row)
        {
            string letters = "";
            for (int n = column; n > 0; n = (n - 1) / 26)
                letters = (char)('A' + (n - 1) % 26) + letters;
            return letters + row.ToString(CultureInfo.InvariantCulture);
        }
    }
}
