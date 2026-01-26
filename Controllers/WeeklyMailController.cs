using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.PublishedModels;
using Umbraco.Cms.Web.Common.UmbracoContext;
using website.Models;
using website.Services;

namespace website.Controllers
{
    public class WeeklyMailController : UmbracoApiController
    {
        private readonly IMemberService _memberService;
        private readonly IEmailService _emailService;
        private readonly IRazorViewToStringRenderer _viewRenderer;
        private readonly IPublishedValueFallback _fallback;
        private readonly IConfiguration _config;
        private readonly ILogger<WeeklyMailController> _logger;
        private readonly UmbracoHelper _helper;

        public WeeklyMailController(
            IMemberService memberService,
            IEmailService emailService,
            IRazorViewToStringRenderer viewRenderer,
            IPublishedValueFallback fallback,
            IConfiguration config,
            ILogger<WeeklyMailController> logger,
            UmbracoHelper helper)
        {
            _memberService = memberService;
            _emailService = emailService;
            _viewRenderer = viewRenderer;
            _fallback = fallback;
            _config = config;
            _logger = logger;
            _helper = helper;
        }

        [HttpGet]
        public async Task<IActionResult> Send(string key)
        {
      
            var configKey = _config["WeeklyMailSecret"];
            if (string.IsNullOrEmpty(key) || key != configKey)
            {
                return Unauthorized("Invalid Secret Key");
            }

            try
            {
                var parentNode = _helper.Content(1059);
                if (parentNode == null) return NotFound("Event container not found");

                var twoWeeksFromNow = DateTime.Now.AddDays(14);

                var events = parentNode.Children()
                    .Select(m => new Event(m, _fallback))
                    .Where(m => m.EndDate >= DateTime.Now && m.StartDate <= twoWeeksFromNow && !m.Hide)
                    .OrderBy(m => m.StartDate)
                    .Select(child => new EventItem
                    {
                        name = child.Acts,
                        startDate = child.StartDate,
                        endDate = child.EndDate,
                        description = child.Description,
                        venue = child.Venue,
                        url = child.Url(mode: UrlMode.Absolute),
                        link = child.Link
                    })
                    .ToList();

                if (events == null || !events.Any()) {
                    return StatusCode(500, "no events");
                }

            
                var emailHtml = await _viewRenderer.RenderViewToStringAsync("~/Views/Emails/WeeklyEvents.cshtml", events);

                long totalRecords;
                var members = _memberService.GetAll(0, int.MaxValue, out totalRecords);

                var recipients = members
                    .Where(x => !string.IsNullOrEmpty(x.Email))
                    .Select(x => x.Email)
                    .Distinct()
                    .ToList();


                recipients = new Events(parentNode,_fallback).NotifyEmails.ToList();


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