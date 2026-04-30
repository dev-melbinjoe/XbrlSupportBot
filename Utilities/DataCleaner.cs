using Newtonsoft.Json.Linq;
using System.Linq;
using System.Text.RegularExpressions;

namespace XbrlSupportBot.Utilities
{
    public static class DataCleaner
    {
        
        public static string ConvertAdfToText(JToken adf)
        {
            if (adf == null || !adf.HasValues && adf.Type != JTokenType.String)
                return string.Empty;

            // Handle API v2 (Plain text)
            if (adf.Type == JTokenType.String)
                return adf.ToString();

            // Handle API v3 (ADF JSON)
            // Use (string) cast to avoid extra double quotes in the text
            var textNodes = adf.SelectTokens("..text")
                               .Select(t => (string)t)
                               .Where(t => !string.IsNullOrWhiteSpace(t));

            return string.Join(" ", textNodes).Trim();
        }

        public static string CleanContentByKeyword(string rawText, string keyword)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return rawText;


            string contentToClean = rawText;
            // Look for where "RCA:" starts
            // If a keyword was found, start the string from that keyword
            if (!string.IsNullOrEmpty(keyword))
            {
                int keywordIndex = rawText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (keywordIndex != -1)
                {
                    contentToClean = rawText.Substring(keywordIndex);
                }
            }

            // 1. Split at common email headers to remove the 'tail'
            // Stop at common email signatures/headers
            string[] terminators = { "Regards", "From:", "Sent:", "Subject:", "Best Regards" };
            int firstTerminator = terminators
                .Select(t => contentToClean.IndexOf(t, StringComparison.OrdinalIgnoreCase))
                .Where(idx => idx != -1)
                .DefaultIfEmpty(contentToClean.Length)
                .Min();

            // 2. Remove extra whitespace and newlines
            //string cleaned = Regex.Replace(fromRca, @"\s+", " ").Trim();

            //return cleaned;


            return contentToClean.Substring(0, firstTerminator).Trim();
        }
    }
}