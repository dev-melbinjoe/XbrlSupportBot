//using Microsoft.AspNetCore.Mvc;
//using XbrlSupportBot.Services;

//[ApiController]
//[Route("test")]
//public class TestRetrievalController : ControllerBase
//{
//    private readonly TfIdfRetrievalService _retrieval;

//    public TestRetrievalController(IConfiguration cfg)
//    {
//        _retrieval = new TfIdfRetrievalService(cfg);
//    }

//    [HttpGet("retrieve")]
//    public async Task<IActionResult> Get([FromQuery] string q)
//    {
//        var res = await _retrieval.Retrieve(q, 5);
//        return Ok(res);
//    }
//}


using Microsoft.AspNetCore.Mvc;
using XbrlSupportBot.Services;

[ApiController]
[Route("test")]
public class TestRetrievalController : ControllerBase
{
    private readonly TfIdfRetrievalService _retrieval;

    public TestRetrievalController(IConfiguration cfg)
    {
        _retrieval = new TfIdfRetrievalService(cfg);
    }

    [HttpGet("retrieve")]
    public async Task<IActionResult> Get([FromQuery] string q, [FromQuery] bool fulltext = true)
    {
        var res = await _retrieval.RetrieveHybrid(q, topK: 5, useFullText: fulltext);
        return Ok(res.Select(r => new {
            r.DocId,
            r.Score,
            r.KeywordScore,
            r.TfidfScore,
            Preview = r.ChunkTxt.Length > 220 ? r.ChunkTxt[..220] + "..." : r.ChunkTxt
        }));
    }
}