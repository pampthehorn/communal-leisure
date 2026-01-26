namespace website.ViewComponents
{
    using global::website.Models;
    using global::website.Models.Database;
    using global::website.Models.ViewModels;
    using Microsoft.AspNetCore.Mvc;
    using Umbraco.Cms.Core.Logging;
    using Umbraco.Cms.Infrastructure.Persistence;
    using Umbraco.Cms.Web.Common;
    using Umbraco.Cms.Web.Common.PublishedModels;

    namespace website.ViewComponents
    {
        [ViewComponent(Name = "EventOrders")]
        public class EventOrdersViewComponent : ViewComponent
        {
            private readonly IUmbracoDatabaseFactory _databaseFactory;
            private readonly ILogger<EventOrdersViewComponent> _logger;
            private readonly UmbracoHelper _umbracoHelper;

            public EventOrdersViewComponent(
                IUmbracoDatabaseFactory databaseFactory,
                ILogger<EventOrdersViewComponent> logger,
                UmbracoHelper umbracoHelper)
            {
                _databaseFactory = databaseFactory;
                _logger = logger;
                _umbracoHelper = umbracoHelper;
            }

            public async Task<IViewComponentResult> InvokeAsync(Guid eventNodeId)
            {
                var viewModel = new EventOrdersViewModel();
                var eventNode = _umbracoHelper.Content(eventNodeId) as Event;
                if (eventNode == null)
                {
                    return View(viewModel); 
                }

                viewModel.EventName = eventNode.Name;

                try
                {
                    using var db = _databaseFactory.CreateDatabase();


                    var sqlString = @"
    SELECT O.* FROM Orders O
    JOIN Ticket T ON O.Id = T.OrderId
    WHERE T.EventNodeId = @0 AND O.Status = 'Completed'
    GROUP BY O.Id, O.TotalAmount, O.CustomerName, O.CustomerEmail, O.CreatedDate, O.Status, O.StripeSessionId, O.StripeCustomerId
    ORDER BY O.CustomerName";

                    var query = db.SqlContext.Sql(sqlString, eventNodeId);

                    var completedOrders = await db.FetchAsync<OrderModel>(query);

                    if (!completedOrders.Any())
                    {
                        return View(viewModel);
                    }

                    var completedOrderIds = completedOrders.Select(o => o.Id).ToList();

                    var ticketsQuery = db.SqlContext.Sql("WHERE OrderId IN (@0) AND EventNodeId = @1", completedOrderIds, eventNodeId);

                    var allTickets = await db.FetchAsync<TicketModel>(ticketsQuery);

                    var ticketsByOrderId = allTickets.GroupBy(t => t.OrderId)
                                                     .ToDictionary(g => g.Key, g => g.AsEnumerable());

                    foreach (var order in completedOrders)
                    {
                        viewModel.CompletedOrders.Add(new OrderVm
                        {
                            Order = order,
                            Tickets = ticketsByOrderId.GetValueOrDefault(order.Id, new List<TicketModel>())
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching event orders for EventNodeId: {EventNodeId}", eventNodeId);
                }

                return View(viewModel);
            }
        }
    }
}
