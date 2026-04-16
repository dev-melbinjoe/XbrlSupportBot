using System.Net.Http.Headers;
using System.Text;
using JiraExtractor.Services;

namespace JiraExtractor.Services
{
    public class Extractor
    {
        static async Task ExtractJiraDataAsync()
        {
            using var client = new HttpClient();

            // Setup Authentication
            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Email}:{ApiToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

            // JQL: Project = Key AND Status = Done
            // We expand 'comments' to get the full comment thread
            string jql = $"project = '{ProjectKey}' AND status = 'Done'";
            string fields = "summary,priority,comment";
            string url = $"{JiraUrl}/rest/api/2/search?jql={Uri.EscapeDataString(jql)}&fields={fields}";

            var response = await client.GetStringAsync(url);
            var json = JObject.Parse(response);
            var issues = json["issues"];

            Console.WriteLine($"Found {issues?.Count()} 'Done' issues. Filtering for RCA comments...\n");
            Console.WriteLine(new string('-', 80));

            foreach (var issue in issues)
            {
                string key = issue["key"]?.ToString();
                string summary = issue["fields"]?["summary"]?.ToString();
                string priority = issue["fields"]?["priority"]?["name"]?.ToString();

                // Get all comments for this issue
                var comments = issue["fields"]?["comment"]?["comments"];

                if (comments != null)
                {
                    foreach (var comment in comments)
                    {
                        string body = comment["body"]?.ToString();

                        // Check if comment contains "RCA:" (case insensitive)
                        if (!string.IsNullOrEmpty(body) && body.Contains("RCA:", StringComparison.OrdinalIgnoreCase))
                        {
                            PrintIssue(key, summary, priority, body);

                            // Here is where you would call your Database Repository
                            // SaveToDatabase(key, summary, priority, body);
                        }
                    }
                }
            }
        }


        static async Task<List<JiraTicket>> FetchJiraData()
        {
            using var client = new HttpClient();
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Email}:{ApiToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

            // JQL for Done tickets
            string jql = "status = 'Done' AND project = 'YOUR_PROJ'";
            string url = $"{JiraUrl}/rest/api/2/search?jql={Uri.EscapeDataString(jql)}&fields=summary,priority,comment";

            var response = await client.GetStringAsync(url);
            var json = JObject.Parse(response);
            var results = new List<JiraTicket>();

            foreach (var issue in json["issues"])
            {
                var comments = issue["fields"]?["comment"]?["comments"];
                var rca = comments?.FirstOrDefault(c => c["body"].ToString().Contains("RCA:", StringComparison.OrdinalIgnoreCase))?["body"]?.ToString();

                if (rca != null)
                {
                    results.Add(new JiraTicket
                    {
                        Key = issue["key"].ToString(),
                        Summary = issue["fields"]["summary"].ToString(),
                        Priority = issue["fields"]["priority"]["name"].ToString(),
                        RcaComment = rca
                    });
                }
            }
            return results;
        }


        static async Task ExportToExcel()
        {
            var tickets = await FetchJiraData();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("RCA Data");

            // Headers
            worksheet.Cell(1, 1).Value = "Key";
            worksheet.Cell(1, 2).Value = "Summary";
            worksheet.Cell(1, 3).Value = "Priority";
            worksheet.Cell(1, 4).Value = "RCA_Comment";

            int row = 2;
            foreach (var t in tickets)
            {
                worksheet.Cell(row, 1).Value = t.Key;
                worksheet.Cell(row, 2).Value = t.Summary;
                worksheet.Cell(row, 3).Value = t.Priority;
                worksheet.Cell(row, 4).Value = t.RcaComment;
                row++;
            }

            workbook.SaveAs(ExcelPath);
            Console.WriteLine($"Exported {row - 2} tickets to {ExcelPath}. Go verify them!");
        }
        static void PrintIssue(string key, string title, string priority, string rca)
        {
            Console.WriteLine($"TICKET:   [{key}] {title}");
            Console.WriteLine($"PRIORITY: {priority}");
            Console.WriteLine($"RCA DATA: {rca.Trim()}");
            Console.WriteLine(new string('-', 80));
        }

    }
}
