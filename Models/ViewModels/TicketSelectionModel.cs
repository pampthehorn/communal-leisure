namespace website.Models.ViewModels
{
    public class TicketSelectionViewModel
    {
        public Guid EventNodeId { get; set; }
        public List<TicketInput> Tickets { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
    }


    public class TicketInput
    {
        public Guid TicketId { get; set; }
        public int Quantity { get; set; }
    }
}
