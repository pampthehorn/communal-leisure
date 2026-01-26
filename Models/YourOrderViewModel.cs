using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common.PublishedModels;
using website.Models.Database;

namespace website.Models
{
    public class YourOrderViewModel : YourOrder
    {
        public YourOrderViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback) : base(content, publishedValueFallback)
        {
        }
        public OrderModel? Order { get; set; }
        public IEnumerable<TicketModel> Tickets { get; set; } = new List<TicketModel>();

      
    }
}
