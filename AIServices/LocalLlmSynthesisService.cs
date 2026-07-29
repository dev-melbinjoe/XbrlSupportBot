using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using BetalgoChatMessage = Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage;
using BetalgoResponse = Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels.ChatCompletionCreateResponse;

namespace XbrlSupportBot.AIServices
{
    public class LocalLlmSynthesisService : ILocalLlmSynthesisService
    {

        private readonly ILogger<LocalLlmSynthesisService> _logger;
        private readonly string _appName;
        private readonly string _modelAlias;
        private bool _isInitialized = false;
        private IModel? _model;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public LocalLlmSynthesisService(IConfiguration config, ILogger<LocalLlmSynthesisService> logger)
        {
            _logger = logger;
            _appName = config["LocalAI:AppName"] ?? "XbrlSupportBot_OfflineEngine";
            _modelAlias = config["LocalAI:ModelAlias"] ?? "qwen2.5-0.5b";
        }

        public async Task EnsureModelLoadedAsync()
        {
            if (_isInitialized) return;

            await _semaphore.WaitAsync();
            try
            {



                if (!_isInitialized)
                {
                    _logger.LogInformation("Initializing Foundry Local manager for App: {AppName}", _appName);

                    // 1. Initialize the async singleton manager
                    await FoundryLocalManager.CreateAsync(new Configuration { AppName = _appName }, _logger);

                    // 2. Get the model catalog and look up the specific model alias
                    var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
                    _model = await catalog.GetModelAsync(_modelAlias);

                    if (_model == null)
                    {
                        throw new Exception($"Model alias '{_modelAlias}' not found in the local catalog.");
                    }

                    _logger.LogInformation("Downloading/Verifying cache for model: {ModelAlias}", _modelAlias);

                    // 3. Ensure the model is cached locally and spun into memory
                    await _model.DownloadAsync();
                    await _model.LoadAsync();

                    _isInitialized = true;
                    _logger.LogInformation("Local model initialized and ready for offline inference.");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        //public async Task<string> GenerateStepByStepGuideAsync(string description, string rca)
        //{
        //    await EnsureModelLoadedAsync();

        //    if (_model == null)
        //        throw new InvalidOperationException("The local AI model interface failed to bind correctly.");

        //    // 1. Resolve the OpenAI-compliant client wrapper from your model instance metadata
        //    var chatClient = await _model.GetChatClientAsync();

        //    string prompt = $"You are an expert system engineering analyzer. Rewrite the following problem description and its raw Root Cause Analysis (RCA) note into a clean, structured, user-facing markdown step-by-step developer troubleshooting layout.\n\nDescription:\n{description}\n\nRCA Context:\n{rca}\n\nReturn ONLY the structured markdown steps.";

        //    _logger.LogInformation("Sending prompt to local LLM inference engine...");
        //    var sb = new StringBuilder();

        //    try
        //    {
        //        // 2. FIX: Wrap the prompt in a UserChatMessage object and call the official OpenAI SDK streaming method
        //        var messages = new OpenAI.Chat.ChatMessage[]
        //         {
        //            new OpenAI.Chat.UserChatMessage(prompt)
        //         };

        //        IAsyncEnumerable<OpenAI.Chat.StreamingChatCompletionUpdate> streamingUpdate = chatClient.CompleteChatStreamingAsync(messages);

        //        await foreach (OpenAI.Chat.StreamingChatCompletionUpdate update in streamingUpdate)
        //        {
        //            foreach (var part in update.ContentUpdate)
        //            {
        //                if (!string.IsNullOrEmpty(part.Text))
        //                {
        //                    sb.Append(part.Text);
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error occurred during local LLM streaming inference.");
        //        return rca; // Graceful fallback to original RCA text upon generation errors
        //    }

        //    return sb.Length > 0 ? sb.ToString() : rca;
        //}



        public async Task<string> GenerateStepByStepGuideAsync(string description, string rca)
        {
            await EnsureModelLoadedAsync();

            if (_model == null)
                throw new InvalidOperationException("The local AI model interface failed to bind correctly.");

            var chatClient = await _model.GetChatClientAsync();

            string prompt = $$"""
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

            _logger.LogInformation("Sending prompt to local LLM inference engine...");
            var sb = new StringBuilder();

            try
            {
                // 1. Build Betalgo's ChatMessage list
                var messages = new List<BetalgoChatMessage>
                {
                    BetalgoChatMessage.FromUser(prompt)
                };

                // 2. Pass messages AND CancellationToken (fixes CS1501 1-argument overload issue)
                IAsyncEnumerable<BetalgoResponse> streamingUpdate = chatClient.CompleteChatStreamingAsync(messages, CancellationToken.None);

                // 3. Extract text deltas from the Betalgo response model
                await foreach (BetalgoResponse response in streamingUpdate)
                {
                    if (response.Successful)
                    {
                        var choice = response.Choices?.FirstOrDefault();

                        // Check delta first (streaming token), fallback to message content
                        string? chunk = choice?.Delta?.Content ?? choice?.Message?.Content;

                        if (!string.IsNullOrEmpty(chunk))
                        {
                            sb.Append(chunk);
                        }
                    }
                    else if (response.Error != null)
                    {
                        _logger.LogError("Streaming Error: {ErrorMessage}", response.Error.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during local LLM streaming inference.");
                return rca;
            }

            return sb.Length > 0 ? sb.ToString() : rca;
        }

        public async Task<string> GenerateGeneralResponseAsync(string userPrompt)
        {
            await EnsureModelLoadedAsync();
            if (_model == null) throw new InvalidOperationException("Model not loaded.");

            var chatClient = await _model.GetChatClientAsync();

            // Generic system instructions keep the model behaving like a normal AI chatbot
            var messages = new List<BetalgoChatMessage>
                            {
                                BetalgoChatMessage.FromSystem("You are a helpful, clear, and friendly AI assistant. Answer the user's questions accurately using clean markdown formatting."),
                                BetalgoChatMessage.FromUser(userPrompt)
                            };

            var sb = new StringBuilder();
            try
            {
                var streamingUpdate = chatClient.CompleteChatStreamingAsync(messages, CancellationToken.None);
                await foreach (var response in streamingUpdate)
                {
                    if (response.Successful)
                    {
                        var chunk = response.Choices?.FirstOrDefault()?.Delta?.Content
                                 ?? response.Choices?.FirstOrDefault()?.Message?.Content;
                        if (!string.IsNullOrEmpty(chunk)) sb.Append(chunk);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating general response.");
                return "I encountered an issue processing your request locally.";
            }

            return sb.ToString();
        }


    }
}
