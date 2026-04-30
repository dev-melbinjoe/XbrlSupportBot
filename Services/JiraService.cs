using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http.Headers;
using System.Text;
using XbrlSupportBot.Models;
using XbrlSupportBot.Utilities;

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
                $"&fields=description,summary,priority,comment" +
                $"&maxResults=100";

            //url = "https://datatracks.atlassian.net/rest/api/2/search/jql?jql=project%20%3D%20%27SWSUP%27%20AND%20status%20%3D%20%27Done%27&fields=summary,priority,comment&maxResults=100";

            var response = await _client.GetStringAsync(url);
            var json = JObject.Parse(response);


            //if (!response.IsSuccessStatusCode)
            //{
            //    throw new Exception($"Jira error {response.StatusCode}: {content}");
            //}

            // Define keywords that signal useful knowledge
            string[] rcaMarkers = { "RCA", "Root Cause", "Resolution", "Fix", "Solution" };

            var results = new List<JiraTicket>();

            foreach (var issue in json["issues"])
            {
               // Get the raw ADF description and convert to text
                var rawDescription = issue["fields"]?["description"];
                var cleanDescription = DataCleaner.ConvertAdfToText(rawDescription);

                var rawComments = issue["fields"]?["comment"]?["comments"]?
                      .Select(c => DataCleaner.ConvertAdfToText(c["body"]))
                      .ToList();

                string rcaRaw = null;
                string detectedMarker = "";

               
                var workaroundRaw = rawComments?.FirstOrDefault(c => c.Contains("Workaround:", StringComparison.OrdinalIgnoreCase));
                
                //Look specifically for comments containing markers in the WHOLE list first
                var commentWithMarker = rawComments?.FirstOrDefault(c =>
                    rcaMarkers.Any(m => c.Contains(m, StringComparison.OrdinalIgnoreCase)));

                if (commentWithMarker != null)
                {
                    rcaRaw = commentWithMarker;
                    // Identify which marker was used so we can clean it correctly
                    detectedMarker = rcaMarkers.FirstOrDefault(m =>
                        rcaRaw.Contains(m, StringComparison.OrdinalIgnoreCase));
                }
                else if (rawComments?.Any() == true) // ONLY if no marker is found in ANY comment, fall back to the longest one
                {
                    rcaRaw = rawComments.OrderByDescending(c => c.Length).First();
                }

                // Clean using the detected marker (or empty string if we fell back to length)
                var finalRca = DataCleaner.CleanContentByKeyword(rcaRaw, detectedMarker) ?? string.Empty;
                var finalWorkaround = DataCleaner.CleanContentByKeyword(workaroundRaw, "Workaround:");

                // Define a list of "Noise" keywords that indicate a ticket is useless
                string[] noiseKeywords = { "test mail", "ignore this", "test_ticket", "finding available", "analyzing the mentioned issue" };
                bool isNoise = noiseKeywords.Any(nk =>
                                                    issue["fields"]?["summary"]?.ToString().Contains(nk, StringComparison.OrdinalIgnoreCase) == true ||
                                                    finalRca?.Contains(nk, StringComparison.OrdinalIgnoreCase) == true);

                if (!string.IsNullOrEmpty(finalRca) && !isNoise && finalRca.Length > 10)
                {
                    results.Add(new JiraTicket
                    {
                        Key = issue["key"]?.ToString(),
                        Description = cleanDescription,
                        Summary = issue["fields"]?["summary"]?.ToString(),
                        Priority = issue["fields"]?["priority"]?["name"]?.ToString(),
                        RcaComment = finalRca ?? "No specific RCA documented",
                        Workaround = finalWorkaround ?? "No manual workaround available"
                    });
                }
            }

            return results;
        }
    }

}