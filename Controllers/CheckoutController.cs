using Microsoft.AspNetCore.Mvc;
using NPoco;
using Stripe;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common.PublishedModels;
using Event = Umbraco.Cms.Web.Common.PublishedModels.Event;
using Newtonsoft.Json;
namespace website;


[TableName("Ticket")]
[PrimaryKey("Id", AutoIncrement = true)]
public class TicketModel
    {

        [PrimaryKeyColumn(AutoIncrement = true)]
        public int Id { get; set; }
        public Guid EventNodeId { get; set; }
        public Guid TicketId { get; set; }
        public int Quantity { get; set; }
        public string Type { get; set; }
        public string EventName { get; set; }
        public int Cost { get; set; }
        public int OrderId { get; set; }
    }


[TableName("Orders")]
[PrimaryKey("Id", AutoIncrement = true)]
public class OrderModel
{
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    public long TotalAmount { get; set; }

    public string CustomerName { get; set; }

    public string CustomerEmail { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = string.Empty;

    public string StripeSessionId { get; set; } = string.Empty;

    public string StripeCustomerId { get; set; } = string.Empty; // Add this line
}


public class TicketSelectionViewModel
{
    public Guid EventNodeId { get; set; }
    public List<TicketInput> Tickets { get; set; }
    public string CustomerName { get; set; }
    public string CustomerEmail { get; set; }
}

public class OrderVm
{
    public OrderModel? Order { get; set; }
    public IEnumerable<TicketModel> Tickets { get; set; } = new List<TicketModel>();
    public string CustomerName { get; set; }
    public string CustomerEmail { get; set; }
}
public class TicketInput
{
    public Guid TicketId { get; set; }
    public int Quantity { get; set; }
}



public class CheckoutController : SurfaceController
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly ILogger<CheckoutController> _logger;
    private readonly IConfiguration _configuration;
    private readonly UmbracoHelper _umbracoHelper;
    private readonly IPublishedValueFallback _publishedValueFallback;

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
        IPublishedValueFallback publishedValueFallback)
        : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
        _configuration = configuration;
        _umbracoHelper = umbracoHelper;
        _publishedValueFallback = publishedValueFallback;
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
                EventName = eventNode.Name,
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

        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

        //var customerService = new CustomerService();
        //var customer = await customerService.CreateAsync(new CustomerCreateOptions
        //{
        //    Name = storedOrderData.CustomerName,
        //    Email = storedOrderData.CustomerEmail,
        //});

        try
        {

            var description = string.Join(", ", storedOrderData.Tickets
                .Select(t => $"{t.Quantity}x {t.Type} ticket for {t.EventName}"));

            var options = new PaymentIntentCreateOptions
            {
                Amount = totalAmountInCents,
                Currency = "gbp",
                //  Customer = customer.Id, 
                Description = description,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                ReceiptEmail = storedOrderData.CustomerEmail,
                
                //Shipping = new ChargeShippingOptions
                //{
                //    Name = storedOrderData.CustomerName
                //    //         Address = new AddressOptions { Country = "GB", PostalCode = "SW1A 0AA", City = "London", Line1 = "10 Downing Street" }
                //},
                Metadata = new Dictionary<string, string> { { "InternalOrderId", newOrder.Id.ToString() } }
            };

            var service = new PaymentIntentService();
            PaymentIntent paymentIntent = await service.CreateAsync(options);
            newOrder.StripeSessionId = paymentIntent.Id;

      

    //    newOrder.StripeCustomerId = customer.Id;
        using (var db = _databaseFactory.CreateDatabase())
        {
            await db.UpdateAsync(newOrder);
        }

        return new JsonResult(new
        {
            clientSecret = paymentIntent.ClientSecret,
            publishableKey = _configuration["Stripe:PublishableKey"]
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
        using var db = _databaseFactory.CreateDatabase();
        var order = await db.SingleOrDefaultAsync<OrderModel>("WHERE StripeSessionId = @0", paymentIntentId);

        if (order == null)
        {
            _logger.LogWarning("OrderComplete was called with an invalid payment_intent_id: {PaymentIntentId}", paymentIntentId);
            return RedirectToUmbracoPage(_umbracoHelper.ContentAtRoot().First());
        }

        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        var service = new PaymentIntentService();
        var paymentIntent = await service.GetAsync(paymentIntentId);

        if (paymentIntent.Status == "succeeded" && order.Status != "Completed")
        {
            order.Status = "Completed";
            order.CustomerName = paymentIntent.Shipping?.Name ?? "N/A";
            order.CustomerEmail = paymentIntent.ReceiptEmail ?? "N/A";
            await db.UpdateAsync(order);
        }

        var tickets = await db.FetchAsync<TicketModel>("WHERE OrderId = @0", order.Id);
        var viewModel = new OrderVm { Order = order, Tickets = tickets };

  
        return View("YourOrder", viewModel);
    }





}
