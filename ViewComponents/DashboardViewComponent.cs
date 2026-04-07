using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.PublishedModels;
using website.Models;
using website.Helpers;
using website.Models.Database;

namespace website.ViewComponents;

[ViewComponent(Name = "Dashboard")]
public class DashboardViewComponent : ViewComponent
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly ILogger<DashboardViewComponent> _logger;
    private readonly UmbracoHelper _umbracoHelper;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IMemberManager _memberManager;

    public DashboardViewComponent(
        IUmbracoDatabaseFactory databaseFactory,
        ILogger<DashboardViewComponent> logger,
        UmbracoHelper umbracoHelper,
        IPublishedValueFallback publishedValueFallback,
        IMemberManager memberManager)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
        _umbracoHelper = umbracoHelper;
        _publishedValueFallback = publishedValueFallback;
        _memberManager = memberManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var viewModel = new DashboardViewModel();

        var currentUser = await _memberManager.GetCurrentMemberAsync();
        if (currentUser == null)
            return View(viewModel);

        viewModel.MemberName = currentUser.Name ?? currentUser.UserName ?? "";

        try
        {
            var parentNode = _umbracoHelper.Content(1059);
            if (parentNode == null)
                return View(viewModel);

            var allEvents = parentNode.Children()
                .Select(m => new Event(m, _publishedValueFallback))
                .Where(e => e.Organizer != null)
                .ToList();

            // Batch lookup: get unique organizer IDs, resolve once
            var organizerIds = allEvents.Select(e => e.Organizer!.Id).Distinct().ToList();
            var organizerEmails = new Dictionary<int, string>();
            foreach (var id in organizerIds)
            {
                var member = await _memberManager.FindByIdAsync(id.ToString());
                if (member != null)
                    organizerEmails[id] = member.Email ?? "";
            }

            // Filter to this member's events using cached lookups
            var myEvents = allEvents
                .Where(e => organizerEmails.TryGetValue(e.Organizer!.Id, out var email)
                             && email == currentUser.Email)
                .ToList();

            var now = UkDateHelper.NowUk;

            var upcoming = myEvents
                .Where(e => e.EndDate >= now)
                .OrderBy(e => e.StartDate)
                .ToList();

            var past = myEvents
                .Where(e => e.EndDate < now)
                .OrderByDescending(e => e.StartDate)
                .ToList();

            // Batch ticket sales query: fetch all sold data for this member's events in one query
            var eventsWithTickets = myEvents.Where(e => e.Tickets != null && e.Tickets.Any()).ToList();
            var soldByEvent = new Dictionary<Guid, Dictionary<Guid, int>>();

            if (eventsWithTickets.Any())
            {
                using var db = _databaseFactory.CreateDatabase();
                var eventNodeIds = eventsWithTickets.Select(e => e.Key).ToList();

                var soldQuery = db.SqlContext.Sql(@"
                    SELECT T.EventNodeId, T.TicketId, SUM(T.Quantity) AS Sold
                    FROM Ticket T
                    JOIN Orders O ON T.OrderId = O.Id
                    WHERE T.EventNodeId IN (@0) AND O.Status = 'Completed'
                    GROUP BY T.EventNodeId, T.TicketId", eventNodeIds);

                var soldData = await db.FetchAsync<TicketSoldRow>(soldQuery);

                foreach (var row in soldData)
                {
                    if (!soldByEvent.ContainsKey(row.EventNodeId))
                        soldByEvent[row.EventNodeId] = new Dictionary<Guid, int>();
                    soldByEvent[row.EventNodeId][row.TicketId] = row.Sold;
                }
            }

            viewModel.UpcomingEvents = BuildDashboardItems(upcoming, soldByEvent);
            viewModel.PastEvents = BuildDashboardItems(past, soldByEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building dashboard for member");
        }

        return View(viewModel);
    }

    private List<DashboardEventItem> BuildDashboardItems(
        List<Event> events,
        Dictionary<Guid, Dictionary<Guid, int>> soldByEvent)
    {
        var items = new List<DashboardEventItem>();

        foreach (var eventNode in events)
        {
            var eventItem = new EventItem
            {
                name = eventNode.Acts ?? "",
                startDate = eventNode.StartDate,
                endDate = eventNode.EndDate,
                acts = eventNode.Acts ?? "",
                venue = eventNode.Venues != null ? eventNode.Venues.First().Name : eventNode.Venue ?? "",
                venueAddress = eventNode.Venues != null
                    ? eventNode.Venues.First().Value("address") + ", " + eventNode.Venues.First().Value("city") + ", " + eventNode.Venues.First().Value("postcode")
                    : "",
                venueUrl = eventNode.Venues?.FirstOrDefault()?.Url() ?? "",
                city = eventNode.Venues != null ? eventNode.Venues.First().Value<string>("city") ?? "" : "",
                description = eventNode.Description ?? "",
                link = eventNode.Link ?? "",
                status = eventNode.Status ?? "",
                tags = eventNode.Tags?.FirstOrDefault() != null
                    ? eventNode.Tags.Select(m => m.Name).Aggregate((a, b) => a + "," + b)
                    : "",
                poster = eventNode.Poster != null
                    ? new Poster { Url = eventNode.Poster.Url() ?? "" }
                    : new Poster { Url = "images/placeholder.jpg" },
                url = eventNode.Url() ?? ""
            };

            var dashItem = new DashboardEventItem { Event = eventItem };

            if (eventNode.Tickets != null && eventNode.Tickets.Any())
            {
                soldByEvent.TryGetValue(eventNode.Key, out var ticketSales);

                foreach (var ticketElement in eventNode.Tickets)
                {
                    var ticket = new Ticket(ticketElement.Content, _publishedValueFallback);
                    var ticketKey = ticketElement.Content.Key;
                    int sold = 0;
                    ticketSales?.TryGetValue(ticketKey, out sold);

                    dashItem.TicketAllocations.Add(new TicketAllocationSummary
                    {
                        Type = ticket.Type ?? "",
                        Sold = sold,
                        Allocation = ticket.Allocation
                    });

                    dashItem.TotalTicketsSold += sold;
                }
            }

            items.Add(dashItem);
        }

        return items;
    }

    private class TicketSoldRow
    {
        public Guid EventNodeId { get; set; }
        public Guid TicketId { get; set; }
        public int Sold { get; set; }
    }
}
