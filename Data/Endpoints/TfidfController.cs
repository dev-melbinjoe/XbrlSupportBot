using Microsoft.AspNetCore.Mvc;
using XbrlSupportBot.Services;

namespace XbrlSupportBot.Endpoints
{
    [ApiController]
    [Route("tfidf")]
    public class TfidfController : ControllerBase
    {
        private readonly IConfiguration _cfg;

        public TfidfController(IConfiguration cfg) => _cfg = cfg;

        [HttpPost("build")]
        public async Task<IActionResult> Build()
        {
            var connStr = _cfg.GetConnectionString("XbrlDb")!;
            var svc = new TfidfEmbeddingService(maxVocab: 20000);
            await svc.BuildTfIdfAsync(connStr);
            return Ok(new { message = "TF-IDF vocabulary and embeddings rebuilt." });
        }
    }
}