using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using XbrlSupportBot.Utilities;

namespace XbrlSupportBot.Data.Endpoints
{
    [Route("ingest")]
    [ApiController]
    public class IngestionController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        public IngestionController(IConfiguration config) => _configuration = config;

        [HttpPost("csv")]
        public async Task<IActionResult> IngestCsv([FromQuery] string csvPath, [FromQuery] string docType)
        {
            if (!System.IO.File.Exists(csvPath))
                return BadRequest($"CSV not found : {csvPath}");

            var constr = _configuration.GetConnectionString("XbrlDb")!;
            int insertedDocs = 0, insertedChunks = 0;

            var conn = new SqlConnection(constr);
            await conn.OpenAsync();

            foreach (var line in System.IO.File.ReadLines(csvPath).Skip(1))
            {
                var parts = ReUsableUtilities.SplitCsv(line);
                if (parts.Length < 6) continue;

                var doc = new
                {
                    DocType = parts[0],
                    Title = parts[1],
                    Content = parts[2],
                    SourceUri = parts[3],
                    Module = parts[4],
                    Tags = parts[5]
                };


                var docId = await conn.ExecuteScalarAsync<long>(@"
                                                                 INSERT INTO Documents (DocType,Title,Content,SourceUri,Module,Tags)
                                                                 OUTPUT INSERTED.Id
                                                                 VALUES (@DocType,@Title,@Content,@SourceUri,@Module,@Tags);", doc
                                                               );
                insertedDocs++;

                var chunks = ReUsableUtilities.ChunkByLength(doc.Content, 1000, 150);

                for (int i = 0; i < chunks.Count; i++)
                {

                    await conn.ExecuteAsync(@"
                                             INSERT INTO DocChunks (DocId,ChunkIndex,ChunkText,Metadata)
                                             VALUES (@DocId,@Idx,@Text,@Meta);",new {
                                                                                        DocId = docId,
                                                                                        Idx = i,
                                                                                        Text = chunks[i],
                                                                                        Meta = (string?)null
                                                                                     }
                                           );
                    insertedChunks++;

                }
            }
            return Ok(new { insertedDocs, insertedChunks });
        }



    }
}
