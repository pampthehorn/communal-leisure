namespace website.Models;

public class DashboardViewModel
{
    public string MemberName { get; set; } = string.Empty;
    public List<DashboardEventItem> UpcomingEvents { get; set; } = new();
    public List<DashboardEventItem> PastEvents { get; set; } = new();
}

public class DashboardEventItem
{
    public EventItem Event { get; set; } = new();
    public List<TicketAllocationSummary> TicketAllocations { get; set; } = new();
    public int TotalTicketsSold { get; set; }
}
