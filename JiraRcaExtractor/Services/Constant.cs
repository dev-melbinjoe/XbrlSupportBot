namespace JiraExtractor.Services
{
    public class Constant
    {
        private static readonly string JiraUrl = "https://your-domain.atlassian.net";
        private static readonly string Email = "your-email@example.com";
        private static readonly string ApiToken = "YOUR_API_TOKEN_HERE";
        private static readonly string ProjectKey = "PROJ"; // Your Project Key

        private const string JiraUrl = "https://your-domain.atlassian.net";
        private const string ApiToken = "YOUR_TOKEN";
        private const string Email = "your-email@example.com";
        private const string ExcelPath = "JiraRcaExport.xlsx";
    }

    public class JiraTicket
    {
        public string Key { get; set; }
        public string Summary { get; set; }
        public string Priority { get; set; }
        public string RcaComment { get; set; }
    }
}
