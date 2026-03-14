namespace website.Models
{
    public class WeeklyEmailViewModel
    {
        public List<EventItem> UpcomingEvents { get; set; } = new();
        public List<EventItem> RecentlyAddedEvents { get; set; } = new();
    }
}
