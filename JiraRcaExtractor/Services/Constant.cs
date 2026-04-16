namespace JiraExtractor.Services
{
   public static class Constant
    {
        public const string JiraUrl = "https://your-domain.atlassian.net";
        public const string Email = "your-email@example.com";
        public const string ApiToken = "YOUR_TOKEN";
        public const string ProjectKey = "PROJ"; 
        public const string ExcelPath = "JiraRcaExport.xlsx";
    }

    public class JiraTicket
    {
        public string Key { get; set; }
        public string Summary { get; set; }
        public string Priority { get; set; }
        public string RcaComment { get; set; }
    }
}
