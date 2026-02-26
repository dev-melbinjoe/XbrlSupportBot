using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using XbrlSupportBot.Services;

namespace XbrlSupportBot.Data.Endpoints
{

    [ApiController]
    [Route("tfidf")]
    public class TfidfController_26022026 : Controller
    {
        private readonly IConfiguration _config;

        public TfidfController_26022026(IConfiguration config)
        {
            _config = config;
        }


        [HttpPost("build")]
        public async Task<IActionResult> Build()
        {
            try
            {
                var connStr = _config.GetConnectionString("XbrlDb");
                var svc = new TfIdfEmbeddingService();

                await svc.BuildandStoreEmbeddings(connStr);

                return Ok(new { message = "TF-IDF embeddings created for all chunks." });

            }
            catch (Exception ex)
            {
                throw;
            }


        }

    }
}
