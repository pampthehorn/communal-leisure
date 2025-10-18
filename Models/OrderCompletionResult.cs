using website.Models.Database;

namespace website.Models
{
    public class OrderCompletionResult
    {
        public bool Success { get; set; }
        public bool WasAlreadyCompleted { get; set; }
        public OrderModel? Order { get; set; }
        public IEnumerable<TicketModel> Tickets { get; set; } = new List<TicketModel>();
    }
}
