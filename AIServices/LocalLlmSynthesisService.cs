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


            string systemPrompt = """
                You are a helpful, clear, and intelligent AI assistant. 
                Your task is to analyze the user's problem description and additional context, then provide a structured, step-by-step solution using clean Markdown formatting.
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
                //new ChatMessage(ChatRole.System, "You are a helpful, clear, and friendly AI support assistant for XBRL issues. Answer accurately using clean markdown formatting."),
                new ChatMessage(ChatRole.System, "You are a helpful, creative, and friendly general-purpose AI assistant. Provide concise, accurate, and structured answers using clean Markdown formatting."),
                new ChatMessage(ChatRole.System, "You are a helpful and friendly AI support assistant. Answer directly in raw text and standard markdown formatting. NEVER wrap your entire response inside a ``` or ```markdown code block."),
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
