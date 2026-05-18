using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.PublishedModels;
using website.Helpers;
using website.Models;

namespace website.Services
{
    public interface IWeeklyMailService
    {
        Task<WeeklyMailResult> SendAsync(CancellationToken ct = default);
    }

    public record WeeklyMailResult(bool Sent, int Recipients, int SentCount, string? Reason = null);

    public class WeeklyMailService : IWeeklyMailService
    {
        private readonly IMemberService _memberService;
        private readonly IEmailService _emailService;
        private readonly IRazorViewToStringRenderer _viewRenderer;
        private readonly IPublishedValueFallback _fallback;
        private readonly UmbracoHelper _helper;
        private readonly ILogger<WeeklyMailService> _logger;

        public WeeklyMailService(
            IMemberService memberService,
            IEmailService emailService,
            IRazorViewToStringRenderer viewRenderer,
            IPublishedValueFallback fallback,
            UmbracoHelper helper,
            ILogger<WeeklyMailService> logger)
        {
            _memberService = memberService;
            _emailService = emailService;
            _viewRenderer = viewRenderer;
            _fallback = fallback;
            _helper = helper;
            _logger = logger;
        }

        public async Task<WeeklyMailResult> SendAsync(CancellationToken ct = default)
        {
            var parentNode = _helper.Content(1059);
            if (parentNode == null) return new WeeklyMailResult(false, 0, 0, "Event container not found");

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
                    startDate = child.StartDate ?? DateTime.MinValue,
                    endDate = child.EndDate ?? DateTime.MinValue,
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
                    startDate = child.StartDate ?? DateTime.MinValue,
                    endDate = child.EndDate ?? DateTime.MinValue,
                    description = child.Description ?? "",
                    venue = child.Venue ?? "",
                    url = child.Url(mode: UrlMode.Absolute) ?? "",
                    link = child.Link ?? ""
                })
                .ToList();

            if (!upcomingEvents.Any() && !recentlyAddedEvents.Any())
            {
                return new WeeklyMailResult(false, 0, 0, "no events");
            }

            var model = new WeeklyEmailViewModel
            {
                UpcomingEvents = upcomingEvents,
                RecentlyAddedEvents = recentlyAddedEvents
            };

            var emailHtml = await _viewRenderer.RenderViewToStringAsync("~/Views/Emails/WeeklyEvents.cshtml", model);

            long totalRecords;
            var members = _memberService.GetAll(0, int.MaxValue, out totalRecords);

            var recipients = members
                .Where(x => !string.IsNullOrEmpty(x.Email))
                .Select(x => x.Email)
                .Distinct()
                .ToList();

            _logger.LogInformation("Starting Weekly Mailout to {Count} members.", recipients.Count);

            int sentCount = 0;
            foreach (var email in recipients)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await _emailService.SendEmailAsync(email, "Events this week", emailHtml);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send weekly mail to {Email}", email);
                }
            }

            return new WeeklyMailResult(true, recipients.Count, sentCount);
        }
    }
}
