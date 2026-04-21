using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using ClosedXML.Excel;
using XbrlSupportBot.Models;

namespace XbrlSupportBot.Services
{

    public class JiraService : IJiraService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _client;

        public JiraService(IConfiguration config, HttpClient client)
        {
            _config = config;
            _client = client;
        }

        public async Task<List<JiraTicket>> FetchRcaTicketsAsync()
        {
            var jira = _config.GetSection("Jira");

            var auth = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{jira["Email"]}:{jira["ApiToken"]}")
            );

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", auth);


            _client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));


            string jql = $"project = '{jira["ProjectKey"]}' AND status = 'Done'";
            //string url = $"{jira["BaseUrl"]}/rest/api/2/search?jql={Uri.EscapeDataString(jql)}&fields=summary,priority,comment&maxResults=100";



            string url =
                $"{jira["BaseUrl"]}/rest/api/3/search/jql" +
                $"?jql={Uri.EscapeDataString(jql)}" +
                $"&fields=summary,priority,comment" +
                $"&maxResults=100";

            //url = "https://datatracks.atlassian.net/rest/api/2/search/jql?jql=project%20%3D%20%27SWSUP%27%20AND%20status%20%3D%20%27Done%27&fields=summary,priority,comment&maxResults=100";

            var response = await _client.GetStringAsync(url);
            var json = JObject.Parse(response);


            //if (!response.IsSuccessStatusCode)
            //{
            //    throw new Exception($"Jira error {response.StatusCode}: {content}");
            //}


            var results = new List<JiraTicket>();

            foreach (var issue in json["issues"])
            {
                //var comments = issue["fields"]?["comment"]?["comments"];

                var allComments = issue["fields"]?["comment"]?["comments"]?.Select(c => c["body"]?.ToString()).ToList();

                // Identify a Workaround (often mentioned by Testing/Support)
                var workaround = allComments?.FirstOrDefault(c => c.Contains("Workaround:", StringComparison.OrdinalIgnoreCase));

                // Identify the RCA
                var rca = allComments?.FirstOrDefault(c => c.Contains("RCA:", StringComparison.OrdinalIgnoreCase));

                //var rca = comments?
                //    .FirstOrDefault(c => c["body"]?.ToString()
                //    .Contains("RCA:", StringComparison.OrdinalIgnoreCase) == true)?["body"]?.ToString();

                if (!string.IsNullOrEmpty(rca))
                {
                    results.Add(new JiraTicket
                    {
                        //Key = issue["key"]?.ToString(),
                        //Summary = issue["fields"]?["summary"]?.ToString(),
                        //Priority = issue["fields"]?["priority"]?["name"]?.ToString(),
                        //RcaComment = rca,

                        Key = issue["key"]?.ToString(),
                        Summary = issue["fields"]?["summary"]?.ToString(),
                        Priority = issue["fields"]?["priority"]?["name"]?.ToString(),
                        RcaComment = rca ?? "No specific RCA documented",
                        Workaround = workaround ?? "No manual workaround available" // Add this field to your Model
                    });
                }
            }

            return results;
        }
    }

}