using System.Text.RegularExpressions;

namespace XbrlSupportBot.Utilities
{
    public class ReUsableUtilities
    {

        public static string[] SplitCsv(string line)
        {
            // Minimal CSV splitter; replace with CsvHelper for production
            var matches = Regex.Matches(line, "(?:^|,)(\"(?:[^\"]|\"\")*\"|[^,]*)");
            return matches.Select(m => {
                var s = m.Value.StartsWith(",") ? m.Value.Substring(1) : m.Value;
                s = s.Trim();
                if (s.StartsWith("\"") && s.EndsWith("\"")) s = s.Substring(1, s.Length - 2).Replace("\"\"", "\"");
                return s;
            }).ToArray();
        }


        public static List<string> ChunkByLength(string text, int chunkSize, int overlap)
        {
            var res = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return res;

            int start = 0;
            while (start < text.Length)
            {
                int end = Math.Min(text.Length, start + chunkSize);
                res.Add(text[start..end]);
                if (end == text.Length) break;
                start = Math.Max(end - overlap, 0);
            }
            return res;
        }

    }
}
