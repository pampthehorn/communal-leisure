using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NPoco;
using Stripe;
using System.Globalization;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.PublishedModels;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Extensions;
using website.Models.Database;
using website.Models.ViewModels;
using website.Services;
using Event = Umbraco.Cms.Web.Common.PublishedModels.Event;
using Member = Umbraco.Cms.Web.Common.PublishedModels.Member;

namespace website;

public class CheckoutController : SurfaceController
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly ILogger<CheckoutController> _logger;
    private readonly IConfiguration _configuration;
    private readonly UmbracoHelper _umbracoHelper;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IEmailService _emailService;

    public CheckoutController(
        IUmbracoContextAccessor umbracoContextAccessor,
        IUmbracoDatabaseFactory databaseFactory,
        ServiceContext services,
        AppCaches appCaches,
        IProfilingLogger profilingLogger,
        IPublishedUrlProvider publishedUrlProvider,
        ILogger<CheckoutController> logger,
        IConfiguration configuration,
        UmbracoHelper umbracoHelper,
        IEmailService emailService,
        IOrderProcessingService orderProcessingService,
        IPublishedValueFallback publishedValueFallback)
        : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
        _configuration = configuration;
        _umbracoHelper = umbracoHelper;
        _publishedValueFallback = publishedValueFallback;
        _emailService = emailService;
        _orderProcessingService = orderProcessingService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SubmitTicketSelection(TicketSelectionViewModel model)
    {
        var selectedTickets = model.Tickets.Where(t => t.Quantity > 0).ToList();

        if (!selectedTickets.Any())
        {
            return RedirectToUmbracoPage(model.EventNodeId);
        }

        var eventNode = _umbracoHelper.Content(model.EventNodeId) as Event;
        if (eventNode == null)
        {
            return BadRequest("Event not found.");
        }

        var orderTickets = new List<TicketModel>();
        foreach (var selectedTicket in selectedTickets)
        {
            var ticketContent = eventNode.Tickets.FirstOrDefault(t => t.Content.Key == selectedTicket.TicketId)?.Content;
            if (ticketContent == null) continue;

            var ticket = new Ticket(ticketContent, _publishedValueFallback);

            orderTickets.Add(new TicketModel
            {
                EventNodeId = model.EventNodeId,
                TicketId = selectedTicket.TicketId,
                Quantity = selectedTicket.Quantity,
                EventName = $"{eventNode.Name} - {eventNode.StartDate.ToString("yyyy-MM-dd")}",
                Type = ticket.Type,
                Cost = (int)(ticket.Cost * 100)
            });
        }

        var orderData = new OrderVm
        {
            Tickets = orderTickets,
            CustomerName = model.CustomerName,
            CustomerEmail = model.CustomerEmail
        };

        TempData["OrderData"] = JsonConvert.SerializeObject(orderData);

        var checkoutPage = _umbracoHelper.ContentAtRoot().DescendantsOrSelfOfType("checkout").FirstOrDefault();
        if (checkoutPage == null)
        {
            return Content("ERROR: Checkout page not found in Umbraco.");
        }

        return RedirectToUmbracoPage(checkoutPage);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePaymentIntent()
    {
        if (TempData["OrderData"] is not string serializedOrderVm)
        {
            return BadRequest(new { error = "Your session has expired. Please start over." });
        }
        var storedOrderData = JsonConvert.DeserializeObject<OrderVm>(serializedOrderVm);
        if (storedOrderData == null || !storedOrderData.Tickets.Any())
        {
            return BadRequest(new { error = "Your basket is empty." });
        }

        TempData.Keep("OrderData");

        string stripeSecretKey = null;
        string stripePublishableKey = null;

        var eventId = storedOrderData.Tickets.First().EventNodeId;
        var eventPage = _umbracoHelper.Content(eventId) as Event;

        if (eventPage?.Organizer !=null)
        {
            var organizer = eventPage?.Organizer;
            if (!string.IsNullOrWhiteSpace(organizer.Value<string>("stripeSecretKey")))
            {
                stripeSecretKey = organizer.Value<string>("stripeSecretKey");
            }
            if (!string.IsNullOrWhiteSpace(organizer.Value<string>("stripePublicKey")))
            {
                stripePublishableKey = organizer.Value<string>("stripePublicKey");
            }
        }

        long totalAmountInCents = storedOrderData.Tickets.Sum(t => (long)t.Cost * t.Quantity);

        var newOrder = new OrderModel
        {
            TotalAmount = totalAmountInCents,
            CustomerName = storedOrderData.CustomerName,
            CustomerEmail = storedOrderData.CustomerEmail,
            Status = "PendingPayment",
            CreatedDate = DateTime.UtcNow
        };

        try
        {
            using var db = _databaseFactory.CreateDatabase();
            await db.InsertAsync(newOrder);

            foreach (var ticket in storedOrderData.Tickets)
            {
                ticket.OrderId = newOrder.Id;
                await db.InsertAsync(ticket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving initial pending order to the database.");
            return StatusCode(500, new { error = "Could not create order." });
        }

        StripeConfiguration.ApiKey = stripeSecretKey;

        try
        {
            var description = string.Join(", ", storedOrderData.Tickets
                .Select(t => $"{t.Quantity}x {t.Type} ticket for {t.EventName}"));

            var options = new PaymentIntentCreateOptions
            {
                Amount = totalAmountInCents,
                Currency = "gbp",
                Description = description,
                ReceiptEmail = storedOrderData.CustomerEmail,
                PaymentMethodTypes = new List<string> { "card" },
                Metadata = new Dictionary<string, string> { { "InternalOrderId", newOrder.Id.ToString() } }
            };

            var service = new PaymentIntentService();
            PaymentIntent paymentIntent = await service.CreateAsync(options);
            newOrder.StripeSessionId = paymentIntent.Id;

            using (var db = _databaseFactory.CreateDatabase())
            {
                await db.UpdateAsync(newOrder);
            }

            return new JsonResult(new
            {
                clientSecret = paymentIntent.ClientSecret,
                publishableKey = stripePublishableKey
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating Stripe payment intent.");
        }
        return new JsonResult(null);
    }

    [HttpGet]
    public async Task<IActionResult> OrderComplete(
        [FromQuery(Name = "payment_intent")] string paymentIntentId,
        [FromQuery(Name = "payment_intent_client_secret")] string clientSecret)
    {
        var result = await _orderProcessingService.FinalizeOrderAsync(paymentIntentId);

        if (!result.Success)
        {
            _logger.LogWarning("OrderComplete was called with an invalid or failed payment_intent_id: {PaymentIntentId}", paymentIntentId);
            return RedirectToUmbracoPage(_umbracoHelper.ContentAtRoot().First());
        }

        var viewModel = new OrderVm { Order = result.Order, Tickets = result.Tickets };

        return View("YourOrder", viewModel);
    }
}