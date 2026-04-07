using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.PublishedModels;
using website.Models;
using website.Helpers;
using website.Services;

namespace website.Controllers
{
    public class WeeklyMailController : Controller
    {
        private readonly IMemberService _memberService;
        private readonly IEmailService _emailService;
        private readonly IRazorViewToStringRenderer _viewRenderer;
        private readonly IPublishedValueFallback _fallback;
        private readonly IConfiguration _config;
        private readonly ILogger<WeeklyMailController> _logger;
        private readonly UmbracoHelper _helper;
        private readonly IWebHostEnvironment _env;

        public WeeklyMailController(
            IMemberService memberService,
            IEmailService emailService,
            IRazorViewToStringRenderer viewRenderer,
            IPublishedValueFallback fallback,
            IConfiguration config,
            ILogger<WeeklyMailController> logger,
            UmbracoHelper helper,
            IWebHostEnvironment env)
        {
            _memberService = memberService;
            _emailService = emailService;
            _viewRenderer = viewRenderer;
            _fallback = fallback;
            _config = config;
            _logger = logger;
            _helper = helper;
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
                var parentNode = _helper.Content(1059);
                if (parentNode == null) return NotFound("Event container not found");

                var today = UkDateHelper.TodayUk;
                var sevenDaysFromNow = today.AddDays(7);
                var oneWeekAgo = today.AddDays(-7);

                var allEvents = parentNode.Children()
                    .Select(m => new Event(m, _fallback))
                    .Where(m => !m.Hide)
                    .ToList();

                var upcomingEvents = allEvents
                    .Where(m => m.EndDate >= today && m.StartDate < sevenDaysFromNow)
                    .OrderBy(m => m.StartDate)
                    .Select(child => new EventItem
                    {
                        name = child.Acts ?? "",
                        startDate = child.StartDate,
                        endDate = child.EndDate,
                        description = child.Description ?? "",
                        venue = child.Venue ?? "",
                        url = child.Url(mode: UrlMode.Absolute) ?? "",
                        link = child.Link ?? ""
                    })
                    .ToList();

                var recentlyAddedEvents = allEvents
                    .Where(m => m.CreateDate >= oneWeekAgo)
                    .OrderByDescending(m => m.CreateDate)
                    .Select(child => new EventItem
                    {
                        name = child.Acts ?? "",
                        startDate = child.StartDate,
                        endDate = child.EndDate,
                        description = child.Description ?? "",
                        venue = child.Venue ?? "",
                        url = child.Url(mode: UrlMode.Absolute) ?? "",
                        link = child.Link ?? ""
                    })
                    .ToList();

                var model = new WeeklyEmailViewModel
                {
                    UpcomingEvents = upcomingEvents,
                    RecentlyAddedEvents = recentlyAddedEvents
                };

                if (!upcomingEvents.Any() && !recentlyAddedEvents.Any()) {
                    return StatusCode(500, "no events");
                }

                var emailHtml = await _viewRenderer.RenderViewToStringAsync("~/Views/Emails/WeeklyEvents.cshtml", model);

                long totalRecords;
                var members = _memberService.GetAll(0, int.MaxValue, out totalRecords);

                var recipients = members
                    .Where(x => !string.IsNullOrEmpty(x.Email))
                    .Select(x => x.Email)
                    .Distinct()
                    .ToList();


                _logger.LogInformation($"Starting Weekly Mailout to {recipients.Count} members.");

                int sentCount = 0;
                foreach (var email in recipients)
                {
                    try
                    {

                        await _emailService.SendEmailAsync(email, "Events this week", emailHtml);
                        sentCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send to {email}");
                    }
                }

                return Ok($"Process Complete. Sent {sentCount} emails.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in weekly mailout.");
                return StatusCode(500, ex.Message);
            }
        }
    }
}
