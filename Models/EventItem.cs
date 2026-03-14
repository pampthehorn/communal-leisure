namespace website.Models
{


    public class EventItem
    {
        public string name { get; set; } = string.Empty;
        public string acts { get; set; } = string.Empty;
        public string venue { get; set; } = string.Empty;
        public string venueUrl { get; set; } = string.Empty;
        public string city { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string link { get; set; } = string.Empty;
        public string organizer { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public Poster poster { get; set; } = new Poster();
        public string tags { get; set; } = string.Empty;
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
        public string url { get; set; } = string.Empty;

    }

    public class Poster
    {
        public string name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
