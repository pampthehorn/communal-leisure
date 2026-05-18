using Microsoft.AspNetCore.Mvc;
using website.Services;

namespace website.Controllers
{
    public class WeeklyMailController : Controller
    {
        private readonly IWeeklyMailService _weeklyMailService;
        private readonly IConfiguration _config;
        private readonly ILogger<WeeklyMailController> _logger;
        private readonly IWebHostEnvironment _env;

        public WeeklyMailController(
            IWeeklyMailService weeklyMailService,
            IConfiguration config,
            ILogger<WeeklyMailController> logger,
            IWebHostEnvironment env)
        {
            _weeklyMailService = weeklyMailService;
            _config = config;
            _logger = logger;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Send(string key)
        {
            if (!_env.IsProduction())
            {
                return BadRequest("Weekly mail can only be sent from the production environment.");
            }

            var configKey = _config["WeeklyMailSecret"];
            if (string.IsNullOrEmpty(key) || key != configKey)
            {
                return Unauthorized("Invalid Secret Key");
            }

            try
            {
                var result = await _weeklyMailService.SendAsync();
                if (!result.Sent)
                {
                    return StatusCode(500, result.Reason ?? "Send did not run");
                }
                return Ok($"Process Complete. Sent {result.SentCount} of {result.Recipients} emails.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in weekly mailout.");
                return StatusCode(500, ex.Message);
            }
        }
    }
}
