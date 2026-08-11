using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using XbrlSupportBot.AIServices;

namespace XbrlSupportBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ILocalLlmSynthesisService _aiService;

        public ChatController(ILocalLlmSynthesisService aiService)
        {
            _aiService = aiService;
        }

        [HttpPost("generate-guide")]
        public async Task<IActionResult> GenerateGuide([FromBody] ChatRequest request)
        {
            var result = await _aiService.GenerateStepByStepGuideAsync(request.Description, request.Rca);
            return Ok(new { response = result });
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] GeneralChatRequest request)
        {
            var result = await _aiService.GenerateGeneralResponseAsync(request.Prompt);
            return Ok(new { response = result });
        }
    }

    public record ChatRequest(string Description, string Rca);
    public record GeneralChatRequest(string Prompt);
}