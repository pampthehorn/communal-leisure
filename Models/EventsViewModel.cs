using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common.PublishedModels;
using website.Models;

public class EventsViewModel : Events
{
    public EventsViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback) : base(content, publishedValueFallback)
    {
    }

    public List<EventItem> Events { get; set; } = new List<EventItem>();
    public List<string> Tags { get; set; } = new List<string>();
    public List<string> Cities { get; set; } = new List<string>();

    public string SelectedTag { get; set; } = "everything";
    public string SelectedCity { get; set; } = "everywhere";
    public int PageNumber { get; set; } = 1;
}

