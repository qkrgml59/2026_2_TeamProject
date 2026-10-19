using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Hwanjo.MapTools.Editor
{
    //지도 시트의 셀 서식 참조만 바꾸고, 채우기만 다른 서식 변형을 뒤에 추가
    //나머지 ZIP 항목은 그대로 복사하고 압축 해제한 내용을 바이트 단위로 비교해 확인
    internal static class XlsxMapWriter
    {
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static string Apply(ExcelTilemapSource source, MapSyncPlan plan)
        {
            string path = ExcelMapSync.ResolveWorkbookPath(source.workbookPath), directory = Path.GetDirectoryName(path);
            string excelLock = Path.Combine(directory, "~$" + Path.GetFileName(path));
            if (File.Exists(excelLock)) throw new IOException("이 통합문서가 Excel에서 열려 있습니다. 저장 후 해당 통합문서를 닫고 다시 미리보기/적용하세요. 읽기와 Excel→Unity 가져오기는 열어 둔 채 사용할 수 있습니다.");

            //1. 미리보기 이후 원본이 바뀌지 않았는지 확인하고 패치본 생성
            byte[] original = XlsxMapReader.ReadStableFile(path);
            if (XlsxMapReader.Hash(original) != plan.ExcelHash) throw new IOException("Excel이 미리보기 이후 바뀌었습니다. 쓰지 않았습니다.");
            byte[] patched = Patch(original, source, plan);
            string token = Guid.NewGuid().ToString("N");
            string temporary = Path.Combine(directory, "." + Path.GetFileNameWithoutExtension(path) + "." + token + ".pending.xlsx");
            string backupFolder = Path.Combine(directory, ".HwanjoBackups");
            string backup = Path.Combine(backupFolder, Path.GetFileNameWithoutExtension(path) + "." + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "." + token + ".xlsx");
            bool replaced = false;
            try
            {
                //2. 임시 파일에 쓰고 다시 읽어 예상한 셀 결과와 같은지 검증
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(patched, 0, patched.Length);
                    output.Flush(true);
                }
                var check = XlsxMapReader.Read(temporary, source.sheetName, source.cellRange, source.excludedRanges, source.paletteSheetName);
                if (Enumerable.Range(0, check.Kinds.Length).Any(i => !ExcelMapSync.Same(check, plan.Result, i)))
                    throw new InvalidDataException("임시 XLSX를 다시 읽은 셀 결과가 예상과 다릅니다. 원본을 교체하지 않았습니다.");

                //3. 배타적으로 열어 Excel이나 저장 중인 핸들이 있으면 거부. Windows에서는 이 핸들을 닫은 뒤에 File.Replace를 해야 함
                using (var gate = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    using var copy = new MemoryStream();
                    gate.CopyTo(copy);
                    if (XlsxMapReader.Hash(copy.ToArray()) != plan.ExcelHash) throw new IOException("적용 직전 외부 수정 감지. 원본 유지.");
                    if (File.Exists(excelLock)) throw new IOException("Excel에서 통합문서를 열었습니다. 원본 유지.");
                }
                Directory.CreateDirectory(backupFolder);
                if (File.Exists(excelLock) || XlsxMapReader.Hash(XlsxMapReader.ReadStableFile(path)) != plan.ExcelHash)
                    throw new IOException("저장/외부 수정 감지. 다시 미리보기 하세요.");

                //4. 원본을 백업으로 남기며 교체
                File.Replace(temporary, path, backup);
                replaced = true;

                //백업은 실제로 교체된 파일이므로, 마지막 확인과 교체 사이에 끼어든 외부 수정도 감지
                if (XlsxMapReader.Hash(File.ReadAllBytes(backup)) != plan.ExcelHash)
                {
                    if (XlsxMapReader.Hash(XlsxMapReader.ReadStableFile(path)) == XlsxMapReader.Hash(patched))
                    {
                        string rejected = backup + ".rejected.xlsx";
                        File.Replace(backup, path, rejected);
                        throw new IOException("교체 순간의 외부 수정 감지. 실제 교체 대상 원본을 복구했고 생성 결과는 " + rejected + "에 보존했습니다. 다시 비교하세요.");
                    }
                    throw new IOException("교체 직후 다시 외부 수정이 발생했습니다. 현재 파일을 건드리지 않았으며 교체 전 파일은 " + backup + "에 보존했습니다. 파일 상태를 확인하세요.");
                }
                if (XlsxMapReader.Hash(XlsxMapReader.ReadStableFile(path)) != XlsxMapReader.Hash(patched))
                    throw new IOException("적용 직후 파일이 변경됐습니다. 기준은 갱신하지 않았습니다. 백업: " + backup);
                return backup;
            }
            finally
            {
                //이번 호출이 만든 GUID 이름의 임시 파일만 정리 대상
                if (!replaced && File.Exists(temporary) && XlsxMapReader.Hash(File.ReadAllBytes(temporary)) == XlsxMapReader.Hash(patched)) File.Delete(temporary);
            }
        }

        internal static byte[] Patch(byte[] original, ExcelTilemapSource source, MapSyncPlan plan)
        {
            using var archive = new ZipArchive(new MemoryStream(original), ZipArchiveMode.Read);
            if (archive.Entries.Any(e => e.FullName.StartsWith("_xmlsignatures/", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("서명된 통합문서는 역반영하지 않습니다.");
            if (archive.Entries.GroupBy(e => e.FullName, StringComparer.Ordinal).Any(g => g.Count() != 1)) throw new InvalidDataException("중복 XLSX 항목입니다.");

            string sheetPath = XlsxMapReader.FindSheetPath(archive, source.sheetName);
            byte[] sheetBytes = Bytes(archive.GetEntry(sheetPath)), styleBytes = Bytes(archive.GetEntry("xl/styles.xml"));
            string sheet = Text(sheetBytes), styles = Text(styleBytes);
            var styleDoc = XlsxMapReader.ReadXml(archive, "xl/styles.xml");
            var sheetDoc = XlsxMapReader.ReadXml(archive, sheetPath);
            if (sheetDoc.Root.Element(Ns + "sheetProtection") != null) throw new InvalidDataException("보호된 지도 시트에는 쓰지 않습니다.");

            var fills = styleDoc.Root.Element(Ns + "fills").Elements(Ns + "fill").ToList();
            var formats = styleDoc.Root.Element(Ns + "cellXfs").Elements(Ns + "xf").ToList();
            int oldFills = fills.Count, oldFormats = formats.Count;
            var cells = sheetDoc.Descendants(Ns + "c").ToDictionary(c => (string)c.Attribute("r"), StringComparer.OrdinalIgnoreCase);
            var styleForAddress = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            //변경 셀마다 원래 서식을 복사해 채우기만 바꾼 서식을 찾거나 새로 추가
            foreach (var change in plan.Changes.Where(c => c.Apply))
            {
                int i = change.Index;
                if (plan.Result.RegionIds[i] == null || (plan.Result.Kinds[i] != MapCellKind.Open && !ExcelMapSync.Physical(plan.Result.Kinds[i])))
                    throw new InvalidOperationException(change.Address + ": 지역 없는 셀/표식/제외 셀의 채우기는 추측하지 않습니다.");
                if (!cells.TryGetValue(change.Address, out var cell)) throw new InvalidOperationException(change.Address + ": 명시적 원본 셀이 없습니다. 셀/지역 생성은 Excel에서 하세요.");
                int old = (int?)cell.Attribute("s") ?? 0;
                if (old < 0 || old >= formats.Count) throw new InvalidDataException("잘못된 셀 서식");

                string rgb = "FF" + plan.Result.Colors[i].ToString("X6", CultureInfo.InvariantCulture);
                int fill = fills.FindIndex(f =>
                {
                    var p = f.Element(Ns + "patternFill");
                    var fg = p?.Element(Ns + "fgColor");
                    return (string)p?.Attribute("patternType") == "solid" && string.Equals((string)fg?.Attribute("rgb"), rgb, StringComparison.OrdinalIgnoreCase) && fg?.Attribute("tint") == null && fg?.Attribute("theme") == null;
                });
                if (fill < 0)
                {
                    fill = fills.Count;
                    fills.Add(new XElement(Ns + "fill", new XElement(Ns + "patternFill", new XAttribute("patternType", "solid"), new XElement(Ns + "fgColor", new XAttribute("rgb", rgb)), new XElement(Ns + "bgColor", new XAttribute("indexed", 64)))));
                }

                var variant = new XElement(formats[old]);
                variant.SetAttributeValue("fillId", fill);
                variant.SetAttributeValue("applyFill", 1);
                int style = formats.FindIndex(x => XNode.DeepEquals(x, variant));
                if (style < 0)
                {
                    style = formats.Count;
                    formats.Add(variant);
                }
                if (formats.Count > 64000) throw new InvalidDataException("Excel 서식 수가 한도에 가깝습니다. 원본 유지.");
                styleForAddress.Add(change.Address, style);
            }

            //시트 XML은 문자열에서 대상 셀 태그의 s 속성만 바꿔 나머지 원문을 그대로 보존
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            sheet = Regex.Replace(sheet, @"<(?:[A-Za-z_][\w.-]*:)?c\b[^>]*>", match =>
            {
                string tag = match.Value;
                var address = Regex.Match(tag, @"(?<![\w:])r\s*=\s*[""']([^""']+)[""']");
                if (!address.Success || !styleForAddress.TryGetValue(address.Groups[1].Value, out int index)) return tag;
                if (!found.Add(address.Groups[1].Value)) throw new InvalidDataException("중복 지도 셀");
                var attribute = Regex.Match(tag, @"(?<![\w:])s\s*=\s*[""'][^""']*[""']");
                if (attribute.Success) return tag.Substring(0, attribute.Index) + "s=\"" + index + "\"" + tag.Substring(attribute.Index + attribute.Length);
                int at = tag.EndsWith("/>", StringComparison.Ordinal) ? tag.Length - 2 : tag.Length - 1;
                return tag.Insert(at, " s=\"" + index + "\"");
            });
            if (found.Count != styleForAddress.Count) throw new InvalidDataException("일부 셀 채우기를 안전하게 찾지 못했습니다.");
            if (fills.Count > oldFills) styles = Append(styles, "fills", fills.Skip(oldFills), fills.Count);
            if (formats.Count > oldFormats) styles = Append(styles, "cellXfs", formats.Skip(oldFormats), formats.Count);

            //ZIP 다시 쓰기: 시트·스타일만 교체하고 나머지 항목은 원본 바이트 그대로 복사
            using var output = new MemoryStream();
            using (var result = new ZipArchive(output, ZipArchiveMode.Create, true))
                foreach (var entry in archive.Entries)
                {
                    var copy = result.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    copy.LastWriteTime = entry.LastWriteTime;
                    copy.ExternalAttributes = entry.ExternalAttributes;
                    using var stream = copy.Open();
                    byte[] bytes = entry.FullName == sheetPath ? Encoded(sheet, sheetBytes) : entry.FullName == "xl/styles.xml" ? Encoded(styles, styleBytes) : Bytes(entry);
                    stream.Write(bytes, 0, bytes.Length);
                }
            byte[] patched = RestoreZipComment(original, output.ToArray());

            using var confirm = new ZipArchive(new MemoryStream(patched), ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
                if (entry.FullName != sheetPath && entry.FullName != "xl/styles.xml" && !Bytes(entry).SequenceEqual(Bytes(confirm.GetEntry(entry.FullName))))
                    throw new InvalidDataException("지도 외 XLSX 항목이 바뀌었습니다: " + entry.FullName);
            return patched;
        }

        //서식 목록(fills/cellXfs) 끝에 새 요소를 덧붙이고 count 속성을 갱신
        private static string Append(string text, string element, IEnumerable<XElement> additions, int count)
        {
            var opening = Regex.Match(text, @"<(?<prefix>(?:[A-Za-z_][\w.-]*:)?)" + element + @"\b[^>]*>");
            if (!opening.Success) throw new InvalidDataException("셀 서식 요소 없음: " + element);

            string tag = opening.Value;
            if (Regex.IsMatch(tag, @"\bcount\s*="))
                tag = Regex.Replace(tag, @"\bcount\s*=\s*[""'][^""']*[""']", "count=\"" + count + "\"");
            else
                tag = tag.Insert(tag.Length - 1, " count=\"" + count + "\"");
            text = text.Substring(0, opening.Index) + tag + text.Substring(opening.Index + opening.Length);

            string close = "</" + opening.Groups["prefix"].Value + element + ">";
            int index = text.IndexOf(close, StringComparison.Ordinal);
            if (index < 0) throw new InvalidDataException("셀 서식 닫기 요소 없음");
            return text.Insert(index, string.Concat(additions.Select(x => x.ToString(SaveOptions.DisableFormatting))));
        }

        private static byte[] Bytes(ZipArchiveEntry entry)
        {
            if (entry == null) throw new InvalidDataException("XLSX 항목 없음");
            using var s = entry.Open();
            using var m = new MemoryStream();
            s.CopyTo(m);
            return m.ToArray();
        }

        //UTF-8 XML만 처리. 앞의 BOM은 건너뛰고 읽음
        private static string Text(byte[] bytes)
        {
            int offset = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0;
            string text = Utf8.GetString(bytes, offset, bytes.Length - offset);
            var encoding = Regex.Match(text, @"^<\?xml[^>]*encoding\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (encoding.Success && !encoding.Groups[1].Value.Equals("UTF-8", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("UTF-8 XLSX XML만 역반영합니다.");
            return text;
        }

        //원본에 BOM이 있었으면 다시 붙여서 저장
        private static byte[] Encoded(string text, byte[] before)
        {
            byte[] result = Utf8.GetBytes(text);
            return before.Length >= 3 && before[0] == 239 && before[1] == 187 && before[2] == 191 ? new byte[] { 239, 187, 191 }.Concat(result).ToArray() : result;
        }

        //원본 ZIP 끝(End of Central Directory)의 주석을 새 ZIP에 그대로 옮김
        private static byte[] RestoreZipComment(byte[] source, byte[] target)
        {
            int End(byte[] b)
            {
                for (int i = b.Length - 22; i >= Math.Max(0, b.Length - 65557); i--)
                    if (BitConverter.ToUInt32(b, i) == 0x06054b50 && i + 22 + BitConverter.ToUInt16(b, i + 20) == b.Length) return i;
                throw new InvalidDataException("ZIP 끝 정보 오류");
            }
            int from = End(source), to = End(target), length = BitConverter.ToUInt16(source, from + 20);
            Array.Resize(ref target, to + 22 + length);
            target[to + 20] = (byte)length;
            target[to + 21] = (byte)(length >> 8);
            Array.Copy(source, from + 22, target, to + 22, length);
            return target;
        }
    }
}
