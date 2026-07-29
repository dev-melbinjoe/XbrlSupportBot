using Microsoft.AspNetCore.Mvc;
using System;
using System.Text;
using System.Threading.Tasks;
using XbrlSupportBot.AIServices;
using XbrlSupportBot.Services;

namespace XbrlSupportBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XbrlSupportChatController : ControllerBase
    {
        private readonly IJiraService _jiraService;
        private readonly ExcelService _excelService;
        private readonly ILocalLlmSynthesisService _localAiService;

        public XbrlSupportChatController(
            IJiraService jiraService,
            ExcelService excelService,
            ILocalLlmSynthesisService localAiService)
        {
            _jiraService = jiraService;
            _excelService = excelService;
            _localAiService = localAiService;
        }

        public class ChatPromptRequest
        {
            public string Query { get; set; } = string.Empty;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> InteractiveChat([FromBody] ChatPromptRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { Message = "Query input string cannot be empty." });
            }

            try
            {
                // Ensure model layers are active and loaded in host memory
                await _localAiService.EnsureModelLoadedAsync();
                string query = request.Query.Trim();
                string[] greetings = new[] { "hi", "hello", "hey", "greetings", "good morning", "good afternoon" };
                if (greetings.Contains(query.ToLower()))
                {
                    string aiResponse = await _localAiService.GenerateGeneralResponseAsync(query);
                    return Ok(new { response = aiResponse });
                }
                else
                {
                    string aiResponse = await _localAiService.GenerateStepByStepGuideAsync(
                      request.Query,
                      "Interactive Live UI User Session Prompt Context"
                    );
                    return Ok(new { Response = aiResponse });
                }
                    // Generate response by passing prompt context down into the local execution instance
                  

              
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"LLM Execution Error: {ex.Message}" });
            }
        }


        [HttpGet("extract")]
        public async Task<IActionResult> ExtractAndProcess()
        {
            try
            {
                // 1. Warm up/Pre-load the local model file system layers before iteration loop starts
                await _localAiService.EnsureModelLoadedAsync();

                // 2. Fetch technical records using the enhanced backward comment filter loop
                var tickets = await _jiraService.FetchRcaTicketsAsync();
                var limitedTickets = tickets.Take(5).ToList();
                // 3. Process every individual engineering log completely offline inside the server
                foreach (var ticket in limitedTickets)
                {
                    string cleanStructuredGuide = await _localAiService.GenerateStepByStepGuideAsync(
                        ticket.Description,
                        ticket.RcaComment
                    );

                    // Rewrite the raw entry text into the user-facing markdown layout
                    ticket.RcaComment = cleanStructuredGuide;
                }

                // 4. Compile the newly optimized and synthesized data rows into your Excel file output
                _excelService.Export(tickets);

                return Ok(new { Message = "Offline processing completed successfully.", ProcessedCount = tickets.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Pipeline Error: {ex.Message}");
            }
        }
    }
}