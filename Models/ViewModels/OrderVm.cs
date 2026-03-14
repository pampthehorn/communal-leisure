using website.Models.Database;

namespace website.Models.ViewModels
{
    public class OrderVm
    {
        public OrderModel? Order { get; set; }
        public IEnumerable<TicketModel> Tickets { get; set; } = new List<TicketModel>();
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
    }
}
