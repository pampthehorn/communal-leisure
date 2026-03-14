namespace website.ViewComponents;

using global::website.Models;
using global::website.Models.Database;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.PublishedModels;

[ViewComponent(Name = "TicketCounter")]
public class TicketCounterViewComponent : ViewComponent
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly ILogger<TicketCounterViewComponent> _logger;
    private readonly UmbracoHelper _umbracoHelper;
    private readonly IPublishedValueFallback _publishedValueFallback;

    public TicketCounterViewComponent(
        IUmbracoDatabaseFactory databaseFactory,
        ILogger<TicketCounterViewComponent> logger,
        UmbracoHelper umbracoHelper,
        IPublishedValueFallback publishedValueFallback)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
        _umbracoHelper = umbracoHelper;
        _publishedValueFallback = publishedValueFallback;
    }

    public async Task<IViewComponentResult> InvokeAsync(Guid eventNodeId)
    {
        var allocations = new List<TicketAllocationSummary>();

        var eventNode = _umbracoHelper.Content(eventNodeId) as Event;
        if (eventNode?.Tickets == null)
            return View(allocations);

        try
        {
            using var db = _databaseFactory.CreateDatabase();

            var soldQuery = db.SqlContext.Sql(@"
                SELECT T.TicketId, SUM(T.Quantity) AS Quantity
                FROM Ticket T
                JOIN Orders O ON T.OrderId = O.Id
                WHERE T.EventNodeId = @0 AND O.Status = 'Completed'
                GROUP BY T.TicketId", eventNodeId);

            var soldRows = await db.FetchAsync<TicketSoldRow>(soldQuery);
            var soldLookup = soldRows.ToDictionary(r => r.TicketId, r => r.Quantity);

            foreach (var ticketElement in eventNode.Tickets)
            {
                var ticket = new Ticket(ticketElement.Content, _publishedValueFallback);
                if (!ticket.Available) continue;

                soldLookup.TryGetValue(ticketElement.Content.Key, out int sold);

                allocations.Add(new TicketAllocationSummary
                {
                    Type = ticket.Type ?? "",
                    Sold = sold,
                    Allocation = ticket.Allocation
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ticket counts for EventNodeId: {EventNodeId}", eventNodeId);
        }

        return View(allocations);
    }

    public class TicketSoldRow
    {
        public Guid TicketId { get; set; }
        public int Quantity { get; set; }
    }
}
