using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using website.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common.PublishedModels;
using Umbraco.Cms.Web.Common;

public class EventsController : RenderController
{
    private readonly IPublishedValueFallback _ipvfb;
    private readonly UmbracoHelper _umbracoHelper;
    private readonly IConfiguration _config;
    private readonly int _pageSize;


    public EventsController(
        ILogger<EventsController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedValueFallback ipvfb,
        IConfiguration config,
        UmbracoHelper umbracoHelper)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _ipvfb = ipvfb;
        _umbracoHelper = umbracoHelper;
        _config = config;
        _pageSize = int.Parse(_config["eventsPageSize"]);
    }

    public override IActionResult Index()
    {
        var query = HttpContext.Request.Query;

        string selectedTag = query.ContainsKey("SelectedTag") ? query["SelectedTag"].ToString() : "everything";
        string selectedCity = query.ContainsKey("SelectedCity") ? query["SelectedCity"].ToString() : "everywhere";

        string viewMode = query.ContainsKey("ViewMode") ? query["ViewMode"].ToString() : "grid";

        int pageNumber = 1;
        if (query.ContainsKey("PageNumber") && int.TryParse(query["PageNumber"], out var pn))
        {
            pageNumber = pn;
        }

        var model = FetchAndProcessEvents(selectedTag, selectedCity, pageNumber, viewMode).GetAwaiter().GetResult();

        if (pageNumber > 1)
        {
            
            return PartialView("_EventGrid", model.Events);
        }

        return CurrentTemplate(model);
    }

    private async Task<EventsViewModel> FetchAndProcessEvents(string selectedTag, string selectedCity, int pageNumber, string viewMode)
    {
        var model = new EventsViewModel(CurrentPage, _ipvfb)
        {
            SelectedTag = selectedTag,
            SelectedCity = selectedCity,
            PageNumber = pageNumber,
            ViewMode = viewMode
        };

        var events = await GetEvents();

        var allTags = new HashSet<string>();
        var allCities = new HashSet<string>();

        foreach (var e in events)
        {
            foreach (var t in e.tags.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)))
            {
                allTags.Add(t);
            }

            if (!string.IsNullOrEmpty(e.city))
            {
                allCities.Add(e.city.Trim());
            }
        }

        model.Tags = allTags.OrderBy(t => t).ToList();
        model.Cities = allCities.OrderBy(c => c).ToList();

        if (selectedTag != "everything")
        {
            events = events.Where(e => e.tags.Split(',').Select(x => x.Trim()).Contains(selectedTag)).ToList();
        }

        if (selectedCity != "everywhere")
        {
            events = events.Where(e => e.city.Equals(selectedCity, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var today = DateTime.UtcNow.Date;
        events = events.OrderBy(e =>
        {
            DateTime start = e.startDate.ToUniversalTime().Date;
            DateTime end = e.endDate.ToUniversalTime().Date;

            bool isToday = start <= today && end >= today;
            bool notStartedToday = start < today;

            return (
                isToday ? 0 : 1,
                isToday ? (notStartedToday ? 1 : 0) : 0,
                start
            );
        }).ToList();

        model.TotalEvents = events.Count;

        if (viewMode == "list")
        {
            model.Events = events;
        }
        else
        {
            int skip = (pageNumber - 1) * _pageSize;
            model.Events = events.Skip(skip).Take(_pageSize).ToList();
        }

        return model;


        return model;
    }

    public async Task<List<EventItem>> GetEvents()
    {

       
            var events = new List<EventItem>();
            var parentNode = _umbracoHelper.Content(1059);
            var children = parentNode.Children().Select(m => new Event(m, _ipvfb)).Where(m => m.EndDate >= DateTime.Now.AddHours(-1) && !m.Hide);

            var ukTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

            if (parentNode != null)
            {
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
                            venue = child.Venues != null ? child.Venues.First().Name + ", " + child.Venues.First().Value("address") + ", " + child.Venues.First().Value("city") + ", " + child.Venues.First().Value("postcode") : child.Venue,
                            city = child.Venues != null ? child.Venues.First().Value<string>("city") : "",
                            description = child.Description,
                            link = child.Link,
                            status = child.Status,
                            tags = child.Tags?.FirstOrDefault() != null ? child.Tags.Select(m => m.Name).Aggregate((a, b) => a + "," + b) : "",
                            poster = child.Poster != null ? new Poster() { Url = child.Poster.Url() } : new Poster()
                            {
                                Url = "images/placeholder.jpg"
                            },
                            url = child.Url()
                        };
                        events.Add(simpleEvent);
                    }
                    catch (Exception e) { }
                }
            }

            return events;
        

    }
}

