namespace website.Models.ViewModels
{
    public class TicketSelectionViewModel
    {
        public Guid EventNodeId { get; set; }
        public List<TicketInput> Tickets { get; set; } = new List<TicketInput>();
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
    }


    public class TicketInput
    {
        public Guid TicketId { get; set; }
        public int Quantity { get; set; }
    }
}
