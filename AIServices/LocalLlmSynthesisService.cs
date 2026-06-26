using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        public async Task<string> GenerateStepByStepGuideAsync(string description, string rca)
        {
            await EnsureModelLoadedAsync();

            if (_model == null)
                throw new InvalidOperationException("The local AI model interface failed to bind correctly.");

            // 1. Resolve the OpenAI-compliant client wrapper from your model instance metadata
            var chatClient = await _model.GetChatClientAsync();

            string prompt = $"You are an expert system engineering analyzer. Rewrite the following problem description and its raw Root Cause Analysis (RCA) note into a clean, structured, user-facing markdown step-by-step developer troubleshooting layout.\n\nDescription:\n{description}\n\nRCA Context:\n{rca}\n\nReturn ONLY the structured markdown steps.";

            _logger.LogInformation("Sending prompt to local LLM inference engine...");
            var sb = new StringBuilder();

            try
            {
                // 2. FIX: Wrap the prompt in a UserChatMessage object and call the official OpenAI SDK streaming method
                var streamingUpdate = chatClient.CompleteChatStreamingAsync(new ChatMessage[]
                {
                    new UserChatMessage(prompt)
                });

                await foreach (var update in streamingUpdate)
                {
                    // In the official OpenAI SDK, streaming fragments return content chunks in ContentUpdate lists
                    foreach (var part in update.ContentUpdate)
                    {
                        if (!string.IsNullOrEmpty(part.Text))
                        {
                            sb.Append(part.Text);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during local LLM streaming inference.");
                return rca; // Graceful fallback to original RCA text upon generation errors
            }

            return sb.Length > 0 ? sb.ToString() : rca;
        }
    }
}
