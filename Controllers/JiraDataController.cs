using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using XbrlSupportBot.Services;

namespace XbrlSupportBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XbrlSupportBotController : ControllerBase
    {
        private readonly IJiraService _jiraService;
        private readonly ExcelService _excelService;

        public XbrlSupportBotController(IJiraService jiraService, ExcelService excelService)
        {
            _jiraService = jiraService;
            _excelService = excelService;
        }

        [HttpGet("extract")]
        public async Task<IActionResult> Extract()
        {
            try
            {
                var tickets = await _jiraService.FetchRcaTicketsAsync();

                _excelService.Export(tickets);

                return Ok(new { count = tickets.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
