namespace website.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using System.Text;
    using System.Xml.Linq;
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Umbraco.Cms.Web.Common;
    using Umbraco.Cms.Web.Common.PublishedModels;
    using website.Helpers;

    [Route("sitemapxml")]
    public class SitemapController : Controller
    {
        private readonly UmbracoHelper _umbracoHelper;
        private readonly IPublishedValueFallback _ipvfb;

        public SitemapController(UmbracoHelper umbracoHelper, IPublishedValueFallback ipvfb)
        {
            _umbracoHelper = umbracoHelper;
            _ipvfb = ipvfb;
        }

        [HttpGet]
        public IActionResult Index()
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            var urls = new List<XElement>();

            // Homepage / Events listing
            urls.Add(CreateUrlElement(ns, "https://communalleisure.com/", DateTime.UtcNow, "daily", "1.0"));

            // Events
            var eventsNode = _umbracoHelper.Content(1059);
            if (eventsNode != null)
            {
                var events = eventsNode.Children()
                    .Select(m => new Event(m, _ipvfb))
                    .Where(m => m.EndDate >= UkDateHelper.NowUk.AddHours(-1) && !m.Hide);

                foreach (var evt in events)
                {
                    var url = evt.Url(mode: UrlMode.Absolute);
                    if (!string.IsNullOrEmpty(url))
                    {
                        urls.Add(CreateUrlElement(ns, url, evt.UpdateDate, "weekly", "0.8"));
                    }
                }
            }

            // Venues
            var venuesNode = _umbracoHelper.ContentAtRoot()
                .SelectMany(r => r.Descendants())
                .FirstOrDefault(c => c.ContentType.Alias == "venues");

            if (venuesNode != null)
            {
                var venuesUrl = venuesNode.Url(mode: UrlMode.Absolute);
                if (!string.IsNullOrEmpty(venuesUrl))
                {
                    urls.Add(CreateUrlElement(ns, venuesUrl, venuesNode.UpdateDate, "weekly", "0.7"));
                }

                foreach (var venue in venuesNode.Children())
                {
                    var url = venue.Url(mode: UrlMode.Absolute);
                    if (!string.IsNullOrEmpty(url))
                    {
                        urls.Add(CreateUrlElement(ns, url, venue.UpdateDate, "weekly", "0.6"));
                    }
                }
            }

            var sitemap = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(ns + "urlset", urls)
            );

            return Content(sitemap.ToString(), "application/xml", Encoding.UTF8);
        }

        private static XElement CreateUrlElement(XNamespace ns, string url, DateTime lastMod, string changeFreq, string priority)
        {
            return new XElement(ns + "url",
                new XElement(ns + "loc", url),
                new XElement(ns + "lastmod", lastMod.ToString("yyyy-MM-dd")),
                new XElement(ns + "changefreq", changeFreq),
                new XElement(ns + "priority", priority)
            );
        }
    }
}
