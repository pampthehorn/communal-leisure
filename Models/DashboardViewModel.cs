namespace website.Models;

public class DashboardViewModel
{
    public string MemberName { get; set; } = string.Empty;
    public List<DashboardEventItem> UpcomingEvents { get; set; } = new();
    public List<DashboardEventItem> PastEvents { get; set; } = new();
    public PromoterStatus PromoterStatus { get; set; } = PromoterStatus.None;
    public List<PurchasedTicketGroup> MyTickets { get; set; } = new();
}

public class PurchasedTicketGroup
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string EventUrl { get; set; } = string.Empty;
    public DateTime? EventStartDate { get; set; }
    public string Venue { get; set; } = string.Empty;
    public bool IsPastEvent { get; set; }
    public List<PurchasedTicketLine> Lines { get; set; } = new();
}

public class PurchasedTicketLine
{
    public string Type { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public List<string> Codes { get; set; } = new();
}

public enum PromoterStatus
{
    None,
    Pending,
    Approved,
}

public class DashboardEventItem
{
    public EventItem Event { get; set; } = new();
    public List<TicketAllocationSummary> TicketAllocations { get; set; } = new();
    public int TotalTicketsSold { get; set; }
}
