using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common.PublishedModels;
using website;
using website.Models;
using website.Models.ViewModels;

namespace website.Models;
public class EventOrdersViewModel
{
    public List<OrderVm> CompletedOrders { get; set; } = new List<OrderVm>();
    public string EventName { get; set; }
}
