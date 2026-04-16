using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JiraDataController.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JiraDataController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Extract()
        {
            try
            {
                
                await ExtractJiraDataAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                
                return StatusCode(500, ex.Message);
            }
        }
    }
}
