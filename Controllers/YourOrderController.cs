using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Stripe;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.PublishedModels;
using website;
using website.Models;

public class YourOrderController : RenderController
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly IConfiguration _configuration;
    private readonly IPublishedValueFallback _publishedValueFallback;

    public YourOrderController(
        ILogger<RenderController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IUmbracoDatabaseFactory databaseFactory,
        IConfiguration configuration,
        IPublishedValueFallback publihsedValueFallback)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _databaseFactory = databaseFactory;
        _configuration = configuration;
    }

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = new YourOrderViewModel(CurrentPage as YourOrder, _publishedValueFallback);

        string paymentIntentId = HttpContext.Request.Query["payment_intent"];

        if (!string.IsNullOrEmpty(paymentIntentId))
        {
            using var db = _databaseFactory.CreateDatabase();
            var order = await db.SingleOrDefaultAsync<OrderModel>("WHERE StripeSessionId = @0", paymentIntentId);

            if (order != null)
            {
                StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);

                if (paymentIntent.Status == "succeeded" && order.Status != "Completed")
                {
                    order.Status = "Completed";
                    order.CustomerName = paymentIntent.Shipping?.Name ?? order.CustomerName;
                    order.CustomerEmail = paymentIntent.ReceiptEmail ?? order.CustomerEmail;
                    await db.UpdateAsync(order);
                }

                viewModel.Order = order;
                viewModel.Tickets = await db.FetchAsync<TicketModel>("WHERE OrderId = @0", order.Id);
            }
        }

        return CurrentTemplate(viewModel);
    }


}