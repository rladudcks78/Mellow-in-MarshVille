using System;
using System.Collections.Generic;
using System.Text;

public static class CsvUtil
{
    /// <summary>
    /// (호환용) 따옴표(")를 고려해 CSV 한 줄을 콤마로 안전하게 분리합니다.
    /// - 콤마 in quotes 지원
    /// - 따옴표 이스케이프("") 지원
    /// - 단, "줄바꿈이 포함된 필드"는 한 줄 입력만 받으므로 여기서 처리 불가 → ParseCsv를 사용하세요.
    /// </summary>
    public static string[] SplitCsvLine(string line)
    {
        if (line == null) return Array.Empty<string>();

        var list = new List<string>();
        var sb = new StringBuilder();

        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    // "" -> "
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                        continue;
                    }

                    // closing quote
                    inQuotes = false;
                    continue;
                }

                sb.Append(ch);
                continue;
            }
            else
            {
                if (ch == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (ch == ',')
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(ch);
            }
        }

        list.Add(sb.ToString());
        return list.ToArray();
    }

    /// <summary>
    /// CSV 전체 텍스트를 파싱해서 Dictionary 리스트로 반환
    /// - 첫 레코드: 헤더
    /// - #으로 시작하는 레코드는 주석으로 무시(레코드 기준)
    /// - 빈 레코드는 무시
    /// - 따옴표 필드 내부의 콤마/개행/따옴표("") 지원
    /// </summary>
    public static List<Dictionary<string, string>> ParseCsv(string csvText)
    {
        var result = new List<Dictionary<string, string>>();
        if (string.IsNullOrEmpty(csvText)) return result;

        List<string> headers = null;

        foreach (var record in ReadRecords(csvText))
        {
            if (record == null || record.Count == 0) continue;

            // 빈 레코드 스킵
            if (record.Count == 1 && string.IsNullOrWhiteSpace(record[0])) continue;

            // 주석 스킵 (첫 컬럼 기준)
            var first = (record[0] ?? "").Trim();
            if (first.StartsWith("#")) continue;

            // 헤더 확정
            if (headers == null)
            {
                headers = new List<string>(record.Count);
                for (int i = 0; i < record.Count; i++)
                    headers.Add(NormalizeHeader(record[i]));
                continue;
            }

            var row = MakeRow(headers, record);
            if (row.Count > 0) result.Add(row);
        }

        return result;
    }

    /// <summary>
    /// (헤더 배열, 값 배열)로부터 row 딕셔너리를 만듭니다.
    /// - 키는 대소문자 무시
    /// </summary>
    public static Dictionary<string, string> MakeRow(IReadOnlyList<string> headers, IReadOnlyList<string> cols)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (headers == null || cols == null) return row;

        int len = Math.Min(headers.Count, cols.Count);
        for (int i = 0; i < len; i++)
        {
            string key = NormalizeHeader(headers[i]);
            if (string.IsNullOrEmpty(key)) continue;

            string val = cols[i] ?? "";
            if (val.EndsWith("\r", StringComparison.Ordinal)) val = val.TrimEnd('\r');

            row[key] = val;
        }

        return row;
    }

    private static string NormalizeHeader(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Trim();
        if (s.EndsWith("\r", StringComparison.Ordinal)) s = s.TrimEnd('\r');
        return s;
    }

    /// <summary>
    /// "1|2|3" 같은 구분자 리스트를 int 리스트로 파싱합니다.
    /// </summary>
    public static List<int> ParseIntList(string s, char separator = '|')
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(s)) return result;

        var parts = s.Split(separator);
        foreach (var p in parts)
        {
            if (int.TryParse(p.Trim(), out int v))
                result.Add(v);
        }

        return result;
    }

    // =========================
    // Getter 유틸
    // =========================
    public static string GetString(Dictionary<string, string> row, string key, string defaultValue = "")
    {
        if (row == null || string.IsNullOrEmpty(key)) return defaultValue;
        return row.TryGetValue(key, out var v) ? (v ?? defaultValue) : defaultValue;
    }

    public static int GetInt(Dictionary<string, string> row, string key, int defaultValue = 0)
    {
        var s = GetString(row, key, "");
        if (string.IsNullOrWhiteSpace(s)) return defaultValue;
        return int.TryParse(s, out var v) ? v : defaultValue;
    }

    // =========================
    // 내부: 레코드 리더(멀티라인 지원)
    // =========================
    private static IEnumerable<List<string>> ReadRecords(string csvText)
    {
        var record = new List<string>();
        var field = new StringBuilder();

        bool inQuotes = false;
        int i = 0;

        while (i < csvText.Length)
        {
            char ch = csvText[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(ch);
                i++;
                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                i++;
                continue;
            }

            if (ch == ',')
            {
                record.Add(field.ToString());
                field.Clear();
                i++;
                continue;
            }

            if (ch == '\r')
            {
                if (i + 1 < csvText.Length && csvText[i + 1] == '\n')
                    i++;

                record.Add(field.ToString());
                field.Clear();

                yield return record;
                record = new List<string>();

                i++;
                continue;
            }

            if (ch == '\n')
            {
                record.Add(field.ToString());
                field.Clear();

                yield return record;
                record = new List<string>();

                i++;
                continue;
            }

            field.Append(ch);
            i++;
        }

        record.Add(field.ToString());
        yield return record;
    }
}
