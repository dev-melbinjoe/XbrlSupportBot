using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XbrlSupportBot.AIServices
{
    public class LocalLlmSynthesisService : ILocalLlmSynthesisService
    {

        private readonly ILogger<LocalLlmSynthesisService> _logger;
        private readonly IChatClient _chatClient;
        private readonly string _modelName;

        public LocalLlmSynthesisService(IConfiguration config, ILogger<LocalLlmSynthesisService> logger)
        {
            _logger = logger;
            _modelName = config["Ollama:ModelName"] ?? "phi3";
            string endpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434/";

            // Initialize Microsoft.Extensions.AI Ollama ChatClient
            _chatClient = new OllamaChatClient(new Uri(endpoint), _modelName);
        }

        public async Task<string> GenerateStepByStepGuideAsync(string description, string rca)
        {
           

            string systemPrompt = $$"""
                You are an expert L2 Database & Application Support Engineer for Rainbow / ESEF XBRL Software.

                Your Responsibilities:
                1. When a user reports an issue (e.g., "Unable to get output", "Calculation loading issue"), DO NOT give generic troubleshooting steps like checking server logs or cloud console unless asked.
                2. First, map the URL provided in the prompt to the correct Database Server and Database Name using the lookup table provided.
                3. Identify the Root Cause Analysis (RCA) scenario matching the user's error.
                4. Provide the exact, formatted SQL diagnostic queries the engineer needs to run against that specific database to fix the issue.

                Environment Lookup Table:
                - http://10.9.9.68:1800 -> DB Server: 10.9.9.19 | DB Name: Rainbow3_Prod_2 (Rainbow Live)
                - http://10.9.9.68:3000 -> DB Server: 10.9.9.19 | DB Name: Rainbow3_TSR_2 (Rainbow Live TSR)
                - http://10.9.9.119:1800 -> DB Server: 10.9.9.19 | DB Name: Rainbow3_Prod_2 (Rainbow Live Backup)
                - http://10.9.9.29:2000 -> DB Server: 10.9.9.19 | DB Name: Rainbow3_US_UAT (Rainbow UAT)
                - https://rainbowdev.datatracks.eu/ -> DB Server: 10.9.9.26 | DB Name: Rainbow3_US_test1 (Rainbow Test)
                - https://rainbow.datatracks.com/ -> DB Server: 10.9.9.26 | DB Name: Rainbow3_US_DEV (Rainbow Demo)
                - http://10.9.9.135:5000/ -> DB Server: 10.9.9.27 | DB Name: Rainbow3_ESEF_Prod_2 (ESEF Live)
                - http://10.9.9.137:5000 -> DB Server: 10.9.9.27 | DB Name: Rainbow3_ESEF_Prod_2 (ESEF Live Backup)
                - http://10.9.9.135:4000/ -> DB Server: 10.9.9.27 | DB Name: Rainbow3_ESEF_LLYODS (ESEF Live Lloyds)
                - http://10.9.9.17:4000 -> DB Server: 10.9.9.26 | DB Name: Rainbow3_ESEF_UAT (ESEF UAT)
                - http://10.9.9.29:7000/ -> DB Server: 10.9.9.26 | DB Name: Rainbow3_ESEF_DEV (ESEF Test)
                - http://10.9.9.29:9000/ -> DB Server: 10.9.9.26 | DB Name: Rainbow3_ESEF_DEMO (ESEF Demo)
                - http://10.9.9.135:3000 -> DB Server: 10.9.9.27 | DB Name: Rainbow3_ESEF_KVK (ESEF KVK)
                - http://10.9.9.82:4000/ -> DB Server: 10.9.9.27 | DB Name: Rainbow3_ESEF_MICA (ESEF Live MICA)

                Diagnostic Rules for "Unable to generate output":
                Check A (Period Tagging Issue):
                Query: select p.Name from tag t inner join presentation p on t.PresentationId= p.childId and t.RoleId=p.Roleid where t.IsActive=1 and t.workspaceid= and t.periodId not in (select id from period where WorkspaceId='')

                Check B (UnitRef 'Day' issue > 32767):
                DECLARE @WorkspaceId int; SET @WorkspaceId = [WorkspaceID];
                SELECT ISNUMERIC(TagValue) AS IsNum,PR.Name AS RoleName,P.Name AS ElementName,T.TagValue INTO #TempTagValue FROM Tag T JOIN PresentationRole PR ON T.WorkspaceId = PR.WorkspaceId and T.RoleId = PR.ID JOIN Presentation P ON PR.Id = P.RoleId AND T.PresentationId = P.ChildId WHERE PR.WorkspaceId = @WorkspaceId AND P.DataTypeId= 16 AND T.UnitRef = 'Day' AND T.IsActive = 1 AND PR.IsActive = 1 AND P.IsActive = 1;
                SELECT * FROM #TempTagValue WHERE IsNum = 1 AND (SELECT CAST(FLOOR(Replace(TagValue,',','')) AS INT)) > 32767

                Check C (Presentation Tree Line Items without Table):
                DECLARE @WorkspaceId int; SET @WorkspaceId = [WorkspaceID];
                SELECT RoleId,SUM(TableExist) TotalTableCount INTO #TempPresentation FROM (SELECT RoleId,CASE WHEN LabelText like '%Table]' THEN 1 else 0 end as TableExist FROM Presentation where RoleId in (SELECT distinct P.RoleId FROM PresentationRole as PR JOIN Presentation as P ON PR.Id = P.RoleId WHERE WorkspaceId=@WorkspaceId and P.LabelText like '%Line Items]' AND PR.IsActive=1 AND P.IsActive=1) AND IsActive=1) ONE GROUP BY RoleId;
                SELECT PRole.Name as RoleName FROM #TempPresentation TP JOIN PresentationRole PRole ON TP.RoleId= PRole.Id WHERE WorkspaceId = @WorkspaceId AND PRole.IsActive=1 and TotalTableCount = 0

                Check D (Special Characters in Element Name):
                select P.IsActive,P.* from PresentationRole PR join Presentation P ON PR.Id = P.RoleId where WorkspaceId= [WorkspaceID] and ParentId <> 0 and P.Name like '%[^a-zA-Z0-9 !”%#$&”()*+,-./:;<=>?@]%'

                Check E (Section Name Mandatory Cover Check):
                SELECT DocumentId as SectionId,Name as SectionName,OrderId,DocumentTypeId FROM Section WHERE WorkspaceId= [WorkspaceID] and SectionType=3 and ParentId=1256 and IsActive=1

                Check F (Null Decimal Value Check):
                For Rainbow US (ScaleType 8,9,11):
                DECLARE @WorkspaceId int; SET @WorkspaceId = [WorkspaceID];
                select PR.Name as RoleName,P.Name as ElementName,T.TagValue,P.DataTypeId,DT.Value as DataType,INF,T.Decimal from Tag T join PresentationRole PR on T.RoleId = PR.Id join Presentation P on T.PresentationId = P.ChildId and T.RoleId = P.RoleId join DataType DT on DT.Code = P.DataTypeId where PR.WorkspaceId=@WorkspaceId and T.WorkspaceId=@WorkspaceId and PR.IsActive = 1 and T.IsActive = 1 and P.IsActive = 1 and T.Decimal is null and ScaleType in (8,9,11) and P.DataTypeId in (30,36,31,12,8,10,27,2,7,28,22,24,29,100,96,98,97,99,113)

                ALWAYS Structure Your Output Exactly Like This:
                ### 1. Identified Environment & DB Details
                * **DB Server:** [Server IP]
                * **DB Name:** [Database Name]
                * **Target Workspace:** [Extracted Workspace ID/Name]

                ### 2. Probable RCA Candidates
                [List candidate issues based on the user's prompt]

                ### 3. Exact SQL Queries to Run for Diagnosis
                ```sql
                [Formatted SQL queries replacing variables with extracted Workspace ID]
               """;

            var userPrompt = $"Description:\n{description}\n\nRCA Context:\n{rca}";

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            _logger.LogInformation("Sending request to local Ollama (phi3)...");
            var sb = new StringBuilder();

            try
            {
                // Stream response directly from Phi-3
                await foreach (var update in _chatClient.GetStreamingResponseAsync(messages))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        sb.Append(update.Text);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Ollama Phi-3 execution.");
                return rca; // Fallback
            }

            return sb.Length > 0 ? sb.ToString() : rca;
        }

        public async Task<string> GenerateGeneralResponseAsync(string userPrompt)
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "You are a helpful, clear, and friendly AI support assistant for XBRL issues. Answer accurately using clean markdown formatting."),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            var sb = new StringBuilder();
            try
            {
                await foreach (var update in _chatClient.GetStreamingResponseAsync(messages))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        sb.Append(update.Text);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response from Ollama.");
                return "I encountered an issue processing your request via local AI.";
            }

            return sb.ToString();
        }
    }
    
}
