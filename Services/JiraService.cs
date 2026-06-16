using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http.Headers;
using System.Net.Sockets;
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
            //var jira = _config.GetSection("Jira");

            //var auth = Convert.ToBase64String(
            //    Encoding.ASCII.GetBytes($"{jira["Email"]}:{jira["ApiToken"]}")
            //);

            //_client.DefaultRequestHeaders.Authorization =
            //    new AuthenticationHeaderValue("Basic", auth);


            //_client.DefaultRequestHeaders.Accept.Add(
            //        new MediaTypeWithQualityHeaderValue("application/json"));


            //string jql = $"project = '{jira["ProjectKey"]}' AND status = 'Done'";
            ////string url = $"{jira["BaseUrl"]}/rest/api/2/search?jql={Uri.EscapeDataString(jql)}&fields=summary,priority,comment&maxResults=100";



            //string url =
            //    $"{jira["BaseUrl"]}/rest/api/3/search/jql" +
            //    $"?jql={Uri.EscapeDataString(jql)}" +
            //    $"&fields=description,summary,priority,comment" +
            //    $"&maxResults=100";

            ////url = "https://datatracks.atlassian.net/rest/api/2/search/jql?jql=project%20%3D%20%27SWSUP%27%20AND%20status%20%3D%20%27Done%27&fields=summary,priority,comment&maxResults=100";

            //var response = await _client.GetStringAsync(url);
            //var json = JObject.Parse(response);


            ////if (!response.IsSuccessStatusCode)
            ////{
            ////    throw new Exception($"Jira error {response.StatusCode}: {content}");
            ////}

            //// Define keywords that signal useful knowledge
            //string[] rcaMarkers = { "RCA", "Root Cause", "Resolution", "Fix", "Solution" };

            //var results = new List<JiraTicket>();

            //foreach (var issue in json["issues"])
            //{
            //   // Get the raw ADF description and convert to text
            //    var rawDescription = issue["fields"]?["description"];
            //    var cleanDescription = DataCleaner.ConvertAdfToText(rawDescription);

            //    var rawComments = issue["fields"]?["comment"]?["comments"]?
            //          .Select(c => DataCleaner.ConvertAdfToText(c["body"]))
            //          .ToList();

            //    string rcaRaw = null;
            //    string detectedMarker = "";


            //    var workaroundRaw = rawComments?.FirstOrDefault(c => c.Contains("Workaround:", StringComparison.OrdinalIgnoreCase));

            //    //Look specifically for comments containing markers in the WHOLE list first
            //    var commentWithMarker = rawComments?.FirstOrDefault(c =>
            //        rcaMarkers.Any(m => c.Contains(m, StringComparison.OrdinalIgnoreCase)));

            //    if (commentWithMarker != null)
            //    {
            //        rcaRaw = commentWithMarker;
            //        // Identify which marker was used so we can clean it correctly
            //        detectedMarker = rcaMarkers.FirstOrDefault(m =>
            //            rcaRaw.Contains(m, StringComparison.OrdinalIgnoreCase));
            //    }
            //    else if (rawComments?.Any() == true) // ONLY if no marker is found in ANY comment, fall back to the longest one
            //    {
            //        rcaRaw = rawComments.OrderByDescending(c => c.Length).First();
            //    }

            //    // Clean using the detected marker (or empty string if we fell back to length)
            //    var finalRca = DataCleaner.CleanContentByKeyword(rcaRaw, detectedMarker) ?? string.Empty;
            //    var finalWorkaround = DataCleaner.CleanContentByKeyword(workaroundRaw, "Workaround:");

            //    // Define a list of "Noise" keywords that indicate a ticket is useless
            //    string[] noiseKeywords = { "test mail", "ignore this", "test_ticket", "finding available", "analyzing the mentioned issue" };
            //    bool isNoise = noiseKeywords.Any(nk =>
            //                                        issue["fields"]?["summary"]?.ToString().Contains(nk, StringComparison.OrdinalIgnoreCase) == true ||
            //                                        finalRca?.Contains(nk, StringComparison.OrdinalIgnoreCase) == true);
            //    var processedTickets = new List<JiraTicket>();
            //    if (!string.IsNullOrEmpty(finalRca) && !isNoise && finalRca.Length > 10)
            //    {
            //        if (IsHighValueRca(finalRca))
            //        {
            //            processedTickets.Add(new JiraTicket
            //            {
            //                Key = ticket.Key,
            //                Summary = ticket.Summary,
            //                Description = cleanDescription,
            //                RcaComment = cleanRca,
            //                Priority = ticket.Priority
            //            });
            //        }

            //        results.Add(new JiraTicket
            //        {
            //            Key = issue["key"]?.ToString(),
            //            Description = cleanDescription,
            //            Summary = issue["fields"]?["summary"]?.ToString(),
            //            Priority = issue["fields"]?["priority"]?["name"]?.ToString(),
            //            RcaComment = finalRca ?? "No specific RCA documented",
            //            Workaround = finalWorkaround ?? "No manual workaround available"
            //        });
            //    }
            //}

            //return results;


            var jira = _config.GetSection("Jira");
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{jira["Email"]}:{jira["ApiToken"]}"));

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
            _client.DefaultRequestHeaders.Accept.Clear(); // Best practice to clear before adding
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string jql = $"project = '{jira["ProjectKey"]}' AND status = 'Done'";
            //string url = $"{jira["BaseUrl"]}/rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&fields=description,summary,priority,comment&maxResults=100";
            string url = $"{jira["BaseUrl"]}/rest/api/3/search?jql={Uri.EscapeDataString(jql)}&fields=description,summary,priority,comment&maxResults=100";
            var response = await _client.GetStringAsync(url);
            var json = JObject.Parse(response);

            string[] rcaMarkers = { "RCA", "Root Cause", "Fix", "Solution", "root cause analysis" }; // Removed from the array -> (Resolution)
            string[] noiseKeywords = { "test mail", "Test email", "ignore this", "test_ticket", "finding available", "analyzing the mentioned issue", "test" };

            var results = new List<JiraTicket>();

            if (json["issues"] == null) return results;

            foreach (var issue in json["issues"])
            {
                // 1. Get Clean Description
                var rawDescription = issue["fields"]?["description"];
                var cleanDescription = DataCleaner.CleanJiraNoise(DataCleaner.ConvertAdfToText(rawDescription));

                // 2. Extract and Clean Comments
                var rawComments = issue["fields"]?["comment"]?["comments"]?
                              .Select(c => DataCleaner.ConvertAdfToText(c["body"]))
                              .ToList();

                string rcaRaw = null;
                string detectedMarker = "";

                // Look specifically for comments containing markers
                var commentWithMarker = rawComments?.FirstOrDefault(c =>
                    rcaMarkers.Any(m => c.Contains(m, StringComparison.OrdinalIgnoreCase)));

                if (commentWithMarker != null)
                {
                    rcaRaw = commentWithMarker;
                    detectedMarker = rcaMarkers.FirstOrDefault(m => rcaRaw.Contains(m, StringComparison.OrdinalIgnoreCase));
                }
                else if (rawComments?.Any() == true)
                {
                    // Fallback to the longest comment as it's most likely the resolution
                    rcaRaw = rawComments.OrderByDescending(c => c.Length).First();
                }

                // 3. Final Cleaning of RCA and Workaround
                var finalRca = DataCleaner.CleanContentByKeyword(rcaRaw, detectedMarker);
                var workaroundRaw = rawComments?.FirstOrDefault(c => c.Contains("Workaround:", StringComparison.OrdinalIgnoreCase));
                var finalWorkaround = DataCleaner.CleanContentByKeyword(workaroundRaw, "Workaround:");

                // 4. Noise Filter Logic
                string summary = issue["fields"]?["summary"]?.ToString() ?? "";
                bool isNoise = noiseKeywords.Any(nk =>
                    summary.Contains(nk, StringComparison.OrdinalIgnoreCase) ||
                    (finalRca?.Contains(nk, StringComparison.OrdinalIgnoreCase) ?? false));

                // 5. Quality Gate & Add to Results
                // Only include if it has a valid RCA, is not noise, and passes High Value check
                if (!string.IsNullOrEmpty(finalRca) && !isNoise && IsHighValueRca(finalRca))
                {
                    results.Add(new JiraTicket
                    {
                        Key = issue["key"]?.ToString(),
                        Summary = summary,
                        Description = cleanDescription,
                        Priority = issue["fields"]?["priority"]?["name"]?.ToString(),
                        RcaComment = finalRca,
                        Workaround = !string.IsNullOrWhiteSpace(finalWorkaround) ? finalWorkaround : "No manual workaround available"
                    });
                }
            }

            return results;
        }




        #region PostAsync to fetch jira details


        public async Task<List<JiraTicket>> FetchRcaTicketsPostAsync()
        {
           
            var jira = _config.GetSection("Jira");
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{jira["Email"]}:{jira["ApiToken"]}"));

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
            _client.DefaultRequestHeaders.Accept.Clear(); // Best practice to clear before adding
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //string jql = $"project = '{jira["ProjectKey"]}' AND status = 'Done'";
            //string url = $"{jira["BaseUrl"]}/rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&fields=description,summary,priority,comment&maxResults=100";
            string url = $"{jira["BaseUrl"]}/rest/api/3/search/jql";


            var requestBody = new
            {
                jql = $"project = '{jira["ProjectKey"]}' AND status = 'Done'",
                fields = new[] { "description", "summary", "priority", "comment" },
                maxResults = 100
            };


            var jsonBody = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, content);


            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Jira API Error: {response.StatusCode} - {errorContent}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);

            string[] rcaMarkers = { "RCA", "Root Cause", "Solution", "root cause analysis" }; // Removed from the array -> (Resolution,"Fix")
            string[] noiseKeywords = { "test mail", "Test email", "ignore this", "test_ticket", "finding available", "analyzing the mentioned issue", "test" };

            var results = new List<JiraTicket>();

            if (json["issues"] == null) return results;

            foreach (var issue in json["issues"])
            {
                // 1. Get Clean Description
                var rawDescription = issue["fields"]?["description"];
                var cleanDescription = DataCleaner.CleanJiraNoise(DataCleaner.ConvertAdfToText(rawDescription));

                // 2. Extract and Clean Comments
                var rawComments = issue["fields"]?["comment"]?["comments"]?
                              .Select(c => DataCleaner.ConvertAdfToText(c["body"]))
                              .ToList();

                string rcaRaw = null;
                string detectedMarker = "";

                // Look specifically for comments containing markers
                var commentWithMarker = rawComments?.FirstOrDefault(c =>
                    rcaMarkers.Any(m => c.Contains(m, StringComparison.OrdinalIgnoreCase)));

                if (commentWithMarker != null)
                {
                    rcaRaw = commentWithMarker;
                    detectedMarker = rcaMarkers.FirstOrDefault(m => rcaRaw.Contains(m, StringComparison.OrdinalIgnoreCase));
                }
                else if (rawComments?.Any() == true)
                {
                    // Fallback to the longest comment as it's most likely the resolution
                    rcaRaw = rawComments.OrderByDescending(c => c.Length).First();
                }

                // 3. Final Cleaning of RCA and Workaround
                var finalRca = DataCleaner.CleanContentByKeyword(rcaRaw, detectedMarker);
                var workaroundRaw = rawComments?.FirstOrDefault(c => c.Contains("Workaround:", StringComparison.OrdinalIgnoreCase));
                var finalWorkaround = DataCleaner.CleanContentByKeyword(workaroundRaw, "Workaround:");

                // 4. Noise Filter Logic
                string summary = issue["fields"]?["summary"]?.ToString() ?? "";
                bool isNoise = noiseKeywords.Any(nk =>
                    summary.Contains(nk, StringComparison.OrdinalIgnoreCase) ||
                    (finalRca?.Contains(nk, StringComparison.OrdinalIgnoreCase) ?? false));

                // 5. Quality Gate & Add to Results
                // Only include if it has a valid RCA, is not noise, and passes High Value check
                if (!string.IsNullOrEmpty(finalRca) && !isNoise && IsHighValueRca(finalRca))
                {
                    results.Add(new JiraTicket
                    {
                        Key = issue["key"]?.ToString(),
                        Summary = summary,
                        Description = cleanDescription,
                        Priority = issue["fields"]?["priority"]?["name"]?.ToString(),
                        RcaComment = finalRca,
                        Workaround = !string.IsNullOrWhiteSpace(finalWorkaround) ? finalWorkaround : "No manual workaround available"
                    });
                }
            }

            return results;
        }



        #endregion

        private bool IsHighValueRca(string rca)
        {
            // Ignore RCAs that are too short or contain "fluff" phrases
            if (rca.Length < 25) return false;

            string[] uselessPhrases = { "working fine", "issue resolved", "fixed now", "kindly check" };
            if (uselessPhrases.Any(p => rca.ToLower().Contains(p)) && rca.Length < 50)
                return false;

            return true;
        }

    }

}