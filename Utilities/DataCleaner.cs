using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;
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


            string processedText = rawText;
            processedText = WebUtility.HtmlDecode(rawText).Replace("\u00A0", " ");
            // Look for where "RCA:" starts
            // If a keyword was found, start the string from that keyword
            if (!string.IsNullOrEmpty(keyword))
            {
                int keywordIndex = rawText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (keywordIndex != -1)
                {
                    processedText = rawText.Substring(keywordIndex);
                }
            }


            //string[] terminators = { "Regards", "From:", "Sent:", "Subject:", "Best Regards" };
            //int firstTerminator = terminators
            //    .Select(t => contentToClean.IndexOf(t, StringComparison.OrdinalIgnoreCase))
            //    .Where(idx => idx != -1)
            //    .DefaultIfEmpty(contentToClean.Length)
            //    .Min();



            return CleanJiraNoise(processedText);
        }


        public static string CleanJiraNoise(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // 1. Decode HTML Entities (Turns &nbsp; into ' ', &amp; into '&', etc.)
            text = WebUtility.HtmlDecode(text);

            // 2. Replace Unicode Non-breaking spaces (\u00A0) with standard spaces
            text = text.Replace("\u00A0", " ");

            // 1. Remove Email Headers (e.g., From: Joe [mailto:joe@...])
            // This cleans the "tail" of email chains
            text = Regex.Replace(text, @"(From|Sent|To|Cc|Subject|Importance):.*", "", RegexOptions.IgnoreCase);
            string titlesPattern = @"(Chief Technology Officer|Manager|Team Lead|SME|Analyst|Reviewer|Lead|Support Team),?\s?(XBRL Services|Product Testing|Service Delivery|Publishing|SD)?";
            text = Regex.Replace(text, titlesPattern, "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"DataTracks Global", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"www\.datatracks\.com", "", RegexOptions.IgnoreCase);
            // 2. Aggressive Disclaimer Removal
            // Targeted at the specific DataTracks/TaurusQuest legal footers seen in your data
            string[] disclaimers = {
                @"This email and any attachments are confidential.*",
                @"Do not share or use them without TaurusQuest’s approval.*",
                @"If you are not the intended recipient.*",
                @"Please advise sender and delete the mail.*"
            };

            foreach (var pattern in disclaimers)
            {
                text = Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            // 3. Signature Truncation
            // Cuts off everything once a signature phrase is detected
            string[] signatureMarkers = { "Regards", "Thanks", "Best Regards", "Thank you" };
            foreach (var marker in signatureMarkers)
            {
                int markerIndex = text.IndexOf(marker + " ", StringComparison.OrdinalIgnoreCase);
                if (markerIndex == -1) markerIndex = text.IndexOf(marker + ",", StringComparison.OrdinalIgnoreCase);
                if (markerIndex == -1 && text.EndsWith(marker, StringComparison.OrdinalIgnoreCase)) markerIndex = text.Length - marker.Length;

                if (markerIndex != -1)
                {
                    text = text.Substring(0, markerIndex);
                }
            }

            // 4. Remove Specific Technical Noise & Contact Details
           
            text = Regex.Replace(text, @"\+?\d{2,4}[\s\-]\d{2,4}[\s\-]\d{4,10}", ""); // Phone numbers
            text = Regex.Replace(text, @"Ext\s?:\s?\d+", "", RegexOptions.IgnoreCase); // Extensions
            text = Regex.Replace(text, @"<(http|https)://[^>]+>", ""); // Remove bracketed URLs but keep the text
            text = Regex.Replace(text, @"\[Created via e-mail received from:.*\]", "", RegexOptions.IgnoreCase); // Jira system noise

            // 5. Final Formatting
            // Remove multiple spaces, tabs, and mid-text newlines to keep the vector dense
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }

    }
}