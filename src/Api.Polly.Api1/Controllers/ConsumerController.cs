using Api.Polly.Api1.Application;
using Microsoft.AspNetCore.Mvc;

namespace Api.Polly.Api1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConsumerController(LogRetryService logRetryService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetMessages()
        {
            var result = await logRetryService.GetMessagesAsync();

            return Ok(result);
        }
    }
}
