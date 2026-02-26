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
    public async Task<IActionResult> Get([FromQuery] string q)
    {
        var res = await _retrieval.Retrieve(q, 5);
        return Ok(res);
    }
}