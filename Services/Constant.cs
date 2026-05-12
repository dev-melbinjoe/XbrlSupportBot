

namespace XbrlSupportBot.Services
{
   //public static class Constant
   // {
   //     public const string JiraUrl = "https://your-domain.atlassian.net";
   //     public const string Email = "your-email@example.com";
   //     public const string ApiToken = "YOUR_TOKEN";
   //     public const string ProjectKey = "PROJ"; 
   //     public const string ExcelPath = "JiraRcaExport.xlsx";
   // }

    public class JiraTicket
    {
        public string? Key { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Summary { get; set; } = string.Empty;
        public string? Priority { get; set; } = string.Empty;
        public string? RcaComment { get; set; } = string.Empty;
        public string? Workaround { get; set; }

        public string GetSemanticDocument()
        {
            return $"Ticket: {Key}\n" +
                   $"Subject: {Summary}\n" +
                   $"Problem: {Description}\n" +
                   $"Solution: {RcaComment}";
        }
    }

    public class XBRLKnowledgeBase
    {
        public string? Id { get; set; } 
        public string? IssueSummary { get; set; }
        public string? Symptom { get; set; } 
        public string? Resolution { get; set; } 
        public string? Tags { get; set; } // e.g., "Superscript, iXBRL, VIP"


    }
}
