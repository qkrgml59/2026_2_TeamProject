using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Hwanjo.MapTools.Editor
{
    //작업 디렉터리나 PC의 프로젝트 위치가 달라도 같은 지도 원본을 찾도록 경로를 한곳에서 해석
    internal static class ExcelWorkbookPath
    {
        internal static string Resolve(string path, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("XLSX 파일 경로를 지정하세요.");
            string root = Path.GetFullPath(projectRoot);
            path = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
                throw new ArgumentException("드라이브 기준의 모호한 경로 대신 프로젝트 상대 경로나 완전한 절대 경로를 사용하세요.");
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
        }

        internal static string Store(string path, string projectRoot)
        {
            string full = Resolve(path, projectRoot);
            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return full.StartsWith(root, comparison) ? full.Substring(root.Length).Replace('\\', '/') : full;
        }

        internal static string Key(string path, string projectRoot)
        {
            string stored = Store(path, projectRoot);
            //프로젝트 안에서는 드라이브·상위 폴더를 기준에 넣지 않음. 외부 절대 경로의 기존 서명은 유지
            return (Path.IsPathRooted(stored) ? stored : "PROJECT:" + stored).ToUpperInvariant();
        }

        internal static string ReadSignature(string packed)
        {
            RequireBaseline(packed);
            using var input = new MemoryStream(Convert.FromBase64String(packed));
            using var zip = new DeflateStream(input, CompressionMode.Decompress);
            using var reader = new BinaryReader(zip, Encoding.UTF8);
            if (reader.ReadInt32() != 1) throw new InvalidDataException("지원하지 않는 지도 기준 형식입니다.");
            return reader.ReadString();
        }

        internal static string Rebind(string packed, string previousSignature, string nextSignature)
        {
            RequireBaseline(packed);
            using var input = new MemoryStream(Convert.FromBase64String(packed));
            using var zip = new DeflateStream(input, CompressionMode.Decompress);
            using var reader = new BinaryReader(zip, Encoding.UTF8);
            if (reader.ReadInt32() != 1 || reader.ReadString() != previousSignature)
                throw new InvalidDataException("이전 경로의 기준과 일치하지 않아 경로를 이전하지 않았습니다.");
            if (previousSignature == nextSignature) return packed;

            //지역·셀·색상과 미반영 변경의 공통 기준은 재생성하지 않고 서명 뒤의 원본 바이트를 복사
            using var output = new MemoryStream();
            using (var compressed = new DeflateStream(output, CompressionLevel.Optimal, true))
            using (var writer = new BinaryWriter(compressed, Encoding.UTF8, true))
            {
                writer.Write(1);
                writer.Write(nextSignature);
                writer.Flush();
                zip.CopyTo(compressed);
            }
            return Convert.ToBase64String(output.ToArray());
        }

        private static void RequireBaseline(string packed)
        {
            if (string.IsNullOrEmpty(packed) || packed.Length > 20000000)
                throw new InvalidDataException("유효한 지도 기준이 없습니다.");
        }
    }
}
