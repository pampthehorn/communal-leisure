namespace website.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using System.ServiceModel.Syndication;
    using System.Text;
    using System.Xml;
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Umbraco.Cms.Core.Web;
    using Umbraco.Cms.Web.Common;
    using Umbraco.Cms.Web.Common.Controllers;
    using Umbraco.Cms.Web.Common.PublishedModels;
    using website.Models;
    public class RssController : RenderController
    {
        private readonly IPublishedValueFallback _ipvfb;
        private readonly UmbracoHelper _umbracoHelper;

        public RssController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedValueFallback ipvfb,
            UmbracoHelper umbracoHelper)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _ipvfb = ipvfb;
            _umbracoHelper = umbracoHelper;
        }

        public override IActionResult Index()
        {
            var parentNode = _umbracoHelper.Content(1059);

         
            DateTime lastBuildDate = DateTime.UtcNow;

            if (parentNode != null)
            {
                lastBuildDate = parentNode.UpdateDate;

                if (parentNode.Children().Any())
                {
                    var latestChild = parentNode.Children().Max(x => x.UpdateDate);
                    if (latestChild > lastBuildDate)
                    {
                        lastBuildDate = latestChild;
                    }
                }
            }
            var events = GetEventsForRss();

            var feedUrl = CurrentPage.Url(mode: UrlMode.Absolute);

            var feed = new SyndicationFeed(
                "Communal Leisure",
                "DIY gigs and events in Glasgow/Edinburgh",
                new Uri(feedUrl),
                feedUrl,
                lastBuildDate
            );

            var items = new List<SyndicationItem>();

            foreach (var evt in events)
            {
                string title = $"{evt.name} @ {evt.venue} ({evt.startDate:dd MMM})";

                
                StringBuilder sb = new StringBuilder();
                sb.Append($"<p><strong>Acts:</strong> {evt.acts}</p>");
                sb.Append($"<p><strong>Venue:</strong> {evt.venue}</p>");
                sb.Append($"<p><strong>Date:</strong> {evt.startDate:F}</p>"); 
                sb.Append($"<p>{evt.description}</p>");

                if (evt.poster?.Url != null)
                {
                    sb.Append($"<img src='{evt.poster.Url}' style='max-width:300px;' />");
                }

                sb.Append($"<p><a href='{evt.url}'>View Event Details</a></p>");

                var item = new SyndicationItem
                {
                    Title = new TextSyndicationContent(title),
                    Summary = new TextSyndicationContent(sb.ToString(), TextSyndicationContentKind.Html),
                    Id = evt.url,
                    PublishDate = evt.startDate
                };

                item.Links.Add(new SyndicationLink(new Uri(evt.url)));

                if (!string.IsNullOrEmpty(evt.tags))
                {
                    foreach (var tag in evt.tags.Split(','))
                    {
                        item.Categories.Add(new SyndicationCategory(tag.Trim()));
                    }
                }

                items.Add(item);
            }

            feed.Items = items;

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                NewLineHandling = NewLineHandling.Entitize,
                NewLineOnAttributes = true,
                Indent = true
            };

            using (var stream = new MemoryStream())
            {
                using (var xmlWriter = XmlWriter.Create(stream, settings))
                {
                    var rssFormatter = new Rss20FeedFormatter(feed, false);
                    rssFormatter.WriteTo(xmlWriter);
                    xmlWriter.Flush();
                }

                return File(stream.ToArray(), "application/rss+xml; charset=utf-8");
            }
        }

        private List<EventItem> GetEventsForRss()
        {
            var events = new List<EventItem>();

            var parentNode = _umbracoHelper.Content(1059);

            if (parentNode == null) return events;

            var children = parentNode.Children()
                .Select(m => new Event(m, _ipvfb))
                .Where(m => m.EndDate >= DateTime.Now.AddHours(-1) && !m.Hide);

            foreach (var child in children)
            {
                try
                {
                   
                    var simpleEvent = new EventItem
                    {
                        name = child.Acts,
                        startDate = child.StartDate,
                        endDate = child.EndDate,
                        acts = child.Acts,
                        venue = child.Venues != null && child.Venues.Any()
                                ? child.Venues.First().Name + ", " + child.Venues.First().Value("address") + ", " + child.Venues.First().Value("city") + ", " + child.Venues.First().Value("postcode")
                                : child.Venue,
                        city = child.Venues != null && child.Venues.Any() ? child.Venues.First().Value<string>("city") : "",
                        description = child.Description,
                        link = child.Link,
                        status = child.Status,
                        tags = child.Tags?.FirstOrDefault() != null ? child.Tags.Select(m => m.Name).Aggregate((a, b) => a + "," + b) : "",
                        poster = child.Poster != null
                                ? new Poster() { Url = child.Poster.Url(mode: UrlMode.Absolute) }
                                : new Poster() { Url = "" }, 
                        url = child.Url(mode: UrlMode.Absolute)
                    };
                    events.Add(simpleEvent);
                }
                catch (Exception) { }
            }

            return events.OrderBy(x => x.startDate).ToList();
        }
    }
}
