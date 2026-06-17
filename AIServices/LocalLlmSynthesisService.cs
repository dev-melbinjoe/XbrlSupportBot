using System;
using System.Threading.Tasks;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;

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
            _appName = config["LocalAI:AppName"] ?? "XbrlSupportBot_LocalAI";
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
            // Safeguard to guarantee the model runtime layer is hot
            await EnsureModelLoadedAsync();

            if (_model == null)
                throw new InvalidOperationException("The local AI model interface failed to bind correctly.");

            // 4. Retrieve the contextual chat client layer from the model instance
            var chatClient = await _model.GetChatClientAsync();

            // Build structured prompt engineering layout
            string prompt = $"You are an expert system engineering analyzer. Rewrite the following problem description and its raw Root Cause Analysis (RCA) note into a clean, structured, user-facing markdown step-by-step developer troubleshooting layout.\n\nDescription:\n{description}\n\nRCA Context:\n{rca}\n\nReturn ONLY the structured markdown steps.";

            // 5. Execute the query using the native Betalgo message packet
            var response = await chatClient.CompleteChatAsync(new[]
            {
                new ChatMessage { Role = "user", Content = prompt }
            });

            return response.Choices?[0]?.Message?.Content ?? rca;
        }
    }
}
