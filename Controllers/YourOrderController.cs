using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.PublishedModels;
using website.Models;
using website.Services;

public class YourOrderController : RenderController
{

    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IOrderProcessingService _orderProcessingService;

    public YourOrderController(
        ILogger<RenderController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedValueFallback publihsedValueFallback,
        IOrderProcessingService orderProcessingService)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
 
        _orderProcessingService = orderProcessingService;
    }

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = new YourOrderViewModel(CurrentPage as YourOrder, _publishedValueFallback);

        string paymentIntentId = HttpContext.Request.Query["payment_intent"];

        if (!string.IsNullOrEmpty(paymentIntentId))
        {
            var result = await _orderProcessingService.FinalizeOrderAsync(paymentIntentId);

            if (result.Success)
            {
                viewModel.Order = result.Order;
                viewModel.Tickets = result.Tickets;
            }

        }

        return CurrentTemplate(viewModel);
    }

}