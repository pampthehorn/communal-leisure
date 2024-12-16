namespace website.Models
{


    public class EventItem
    {
        public string name { get; set; }
        public string acts { get; set; }
        public string venue { get; set; }
        public string city { get; set; }
        public string description { get; set; }
        public string link { get; set; }
        public string organizer { get; set; }
        public string status { get; set; }
        public Poster poster { get; set; }
        public string tags { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
        public string url { get; set; }

    }

    public class Poster
    {
        public string name { get; set; }
        public string Url { get; set; }
    }
}
