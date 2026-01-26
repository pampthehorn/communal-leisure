namespace website.Controllers
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Mail;
    using System.Text;
    using System.Threading.Tasks;
    using Umbraco.Cms.Core;
    using Umbraco.Cms.Core.Cache;
    using Umbraco.Cms.Core.IO;
    using Umbraco.Cms.Core.Logging;
    using Umbraco.Cms.Core.Models;
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Umbraco.Cms.Core.PropertyEditors;
    using Umbraco.Cms.Core.Routing;
    using Umbraco.Cms.Core.Security;
    using Umbraco.Cms.Core.Services;
    using Umbraco.Cms.Core.Strings;
    using Umbraco.Cms.Core.Web;
    using Umbraco.Cms.Infrastructure.Persistence;
    using Umbraco.Cms.Web.Common;
    using Umbraco.Cms.Web.Common.PublishedModels;
    using Umbraco.Cms.Web.Website.Controllers;
    using Umbraco.Extensions;
    using website.Services;
    using Udi = Umbraco.Cms.Core.Udi;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Formats.Jpeg;

    public class EventSurfaceController : SurfaceController
        {
        private readonly IContentService _contentService;
        private readonly IMediaService _mediaService;
        private readonly IUmbracoContextAccessor _contextAccessor;
        private readonly MediaFileManager _mediaFileManager;
        private readonly MediaUrlGeneratorCollection _mediaUrlGeneratorCollection;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
        private readonly IMemberManager _memberManager;
        private readonly IConfiguration _configuration;
        private readonly UmbracoHelper _umbracoHelper;
        private readonly IPublishedValueFallback _ipvfb;
        private readonly IEmailService _emailService;
        private readonly ILogger<EventSurfaceController> _logger;



        public EventSurfaceController(IContentService contentService, IMediaService mediaService, MediaFileManager mediaFileManager, MediaUrlGeneratorCollection mediaUrlGeneratorCollection, IShortStringHelper shortStringHelper, IContentTypeBaseServiceProvider contentTypeBaseServiceProvider, IUmbracoContextAccessor umbracoContextAccessor,
        IUmbracoDatabaseFactory databaseFactory,
        ServiceContext services,
        AppCaches appCaches,
        IProfilingLogger profilingLogger,
        IPublishedUrlProvider publishedUrlProvider,
        IMemberManager memberManager,
        IConfiguration configuration,
        IEmailService emailService,
        UmbracoHelper umbracoHelper,
        IPublishedValueFallback ipvfb,
        ILogger<EventSurfaceController> logger)
        : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
            {
            _contentService = contentService;
            _mediaService = mediaService;
            _mediaFileManager = mediaFileManager;
            _mediaUrlGeneratorCollection = mediaUrlGeneratorCollection;
            _shortStringHelper = shortStringHelper;
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
            _memberManager = memberManager;
            _configuration = configuration;
            _umbracoHelper = umbracoHelper;
            _ipvfb = ipvfb;
            _emailService = emailService;
            _logger = logger;
        }


        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExtractFromPoster(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                string base64Image;
                using (var image = SixLabors.ImageSharp.Image.Load(file.OpenReadStream()))
                {
                    if (image.Width > 1000)
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(1000, 0),
                            Mode = ResizeMode.Max
                        }));
                    }

                    using (var ms = new MemoryStream())
                    {
                        await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 75 });
                        base64Image = Convert.ToBase64String(ms.ToArray());
                    }
                }

                var apiKey = _configuration["Gemini:ApiKey"];
                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent?key={apiKey}";

                var todayStr = DateTime.Now.ToString("yyyy-MM-dd");

                var promptText = $@"
    Context: Today is {todayStr}.
    Analyze this event flyer. Extract details into a raw JSON object. 
    Do NOT use markdown formatting (no ```json blocks). Return ONLY the JSON string.
    
    Fields required:
    - acts: (String) The names of the bands/artists.
    - venue: (String) The name of the venue.
    - startDate: (String) The date and time in strict ISO format 'yyyy-MM-ddTHH:mm'.
    - description: (String) A brief summary including entry price/ticket info.
    - link: (String) Any specific URL or website address visible on the poster.
    - tag: (String) Choose exactly ONE of these categories based on the event type: 'Gig', 'Club', or 'Activity'.
      - 'Gig': Live bands, acoustic sets, concerts.
      - 'Club': DJs, raves, late night dance events.
      - 'Activity': Workshops, quizzes, markets, or non-music events.
";

                var payload = new
                {
                    contents = new[]
                    {
                new
                {
                    parts = new object[]
                    {
                        new { text = promptText },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = file.ContentType,
                                data = base64Image
                            }
                        }
                    }
                }
            }
                };

                using (var client = new HttpClient())
                {
                    var jsonPayload = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(apiUrl, content);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return StatusCode((int)response.StatusCode, "Error from AI provider");
                    }

                    dynamic result = JsonConvert.DeserializeObject(responseString);
                    string extractedText = result.candidates[0].content.parts[0].text;

                    extractedText = extractedText.Replace("```json", "").Replace("```", "").Trim();

                    dynamic eventData = JsonConvert.DeserializeObject(extractedText);

                    if (eventData != null && eventData.startDate != null)
                    {
                        string aiDateString = (string)eventData.startDate;

                        if (DateTime.TryParse(aiDateString, out DateTime aiDate))
                        {
                            var now = DateTime.Now;

                            var fixedDate = new DateTime(
                                now.Year,
                                aiDate.Month,
                                aiDate.Day,
                                aiDate.Hour,
                                aiDate.Minute,
                                0
                            );

             
                            if (fixedDate < now.AddDays(-1))
                            {
                                fixedDate = fixedDate.AddYears(1);
                            }

                            eventData.startDate = fixedDate.ToString("yyyy-MM-ddTHH:mm");
                        }
                    }

                    return Content(JsonConvert.SerializeObject(eventData), "application/json");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract info from poster");
                return StatusCode(500, "Error processing image");
            }
        }



        [HttpPost]
            public async Task<IActionResult> SubmitEventAsync(IFormFile poster, DateTime startDate, DateTime endDate, string acts, string venue, string description, string link, string organizer, string tags, string email)
        {
            try
                {

                if (poster == null) { throw new Exception("no poster"); }
                if (acts == null) { throw new Exception("no acts"); }


                var venues = _umbracoHelper.Content(1059).Siblings().FirstOrDefault(m => m.IsDocumentType(Venues.ModelTypeAlias)).DescendantsOfType(Venue.ModelTypeAlias).Select(m => new Venue(m, _ipvfb));
                var venueMatch = venues.FirstOrDefault(m => m.Name + ", " + m.City == venue);
        
                var eventNode = _contentService.Create(startDate.ToString("yyyy-MM-dd") + " " + acts,1059, Event.ModelTypeAlias ,-1);
                    organizer = "";
                    var currentMember = await _memberManager.GetCurrentMemberAsync();
                    if (currentMember != null) {
                        organizer = currentMember.Id;
                        eventNode.SetValue("startDate", startDate);
                    }
                    if (poster != null && poster.Length > 0)
                    {
                        IMedia media = _mediaService.CreateMedia(poster.FileName, Constants.System.Root, Constants.Conventions.MediaTypes.Image);
                    using (Stream stream = poster.OpenReadStream())
                    {

                        media.SetValue("umbracoFile", stream);
                        media.SetValue(_mediaFileManager, _mediaUrlGeneratorCollection, _shortStringHelper, _contentTypeBaseServiceProvider, Constants.Conventions.Media.File, poster.FileName, stream);
                        _mediaService.Save(media);
                    }

                    eventNode.SetValue("poster", media.GetUdi());
                    }
                string tagsToSave = "";
                if (!tags.IsNullOrWhiteSpace()) {
                    var splitTags = tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(m => Udi.Create(Constants.UdiEntityType.Document,new Guid(m)).UriValue.ToString()).ToList();
                    tagsToSave = splitTags.Aggregate((a, b) => a + "," + b);
                }

                    eventNode.SetValue("startDate", startDate);
                    eventNode.SetValue("endDate", endDate);
                    eventNode.SetValue("acts", acts);
                if (venueMatch != null)
                {
                    eventNode.SetValue("venues", Udi.Create(Constants.UdiEntityType.Document, venueMatch.Key));
                }
                else {
                    eventNode.SetValue("venues", null);
                }
                eventNode.SetValue("venue", venue);
                    eventNode.SetValue("description", description);
                    eventNode.SetValue("link", link);
                    eventNode.SetValue("organizer", organizer);
                    eventNode.SetValue("tags", tagsToSave);
                eventNode.SetValue("status", JsonConvert.SerializeObject(new[] { "Going Ahead" }));
                
                string editKey = Guid.NewGuid().ToString();
                eventNode.SetValue("editKey", editKey);
                eventNode.SetValue("email", email);
               
                    _contentService.SaveAndPublish(eventNode);
               
                   
                  var eventsNode = new Events(_umbracoHelper.ContentAtRoot().FirstOrDefault(m => m.ContentType.Alias == "events"), _ipvfb);
                    var editLink = _umbracoHelper.Content(eventNode.Id).Url(mode: UrlMode.Absolute) +"?key=" + editKey;

                var editLinkText = $"<br/><br/>Once published you will be able to edit with this link: {editLink}";
                if (currentMember == null)
                {
                    _contentService.Unpublish(eventNode);
                }

                string subject = $"New Event Submitted: {acts} on {startDate.ToShortDateString()}";
                string body = $"A new event has been submitted.<br/><br/><b>Acts:</b> {acts}<br/><b>Date:</b> {startDate}{editLinkText}";
                string toAddress = "comlesweb@gmail.com"; 

                await _emailService.SendEmailAsync(toAddress, subject, body, eventsNode.NotifyEmails);


                TempData["SuccessMessage"] = eventNode.Name + " submitted successfully.";
                return RedirectToCurrentUmbracoPage();
                }
                catch (Exception ex)
                {
                TempData["ErrorMessage"] = "Error submitting event. Please try again.";
                return CurrentUmbracoPage();
                }
            }

        [HttpPost]
        public async Task<IActionResult> EditEventAsync(int nodeId, IFormFile poster, DateTime startDate, DateTime endDate, string acts, string venue, string description, string link, string organizer, string tags, string email, string status)
        {

            try
            {

                if (acts == null) { throw new Exception("no acts"); }
              if (venue == null) { throw new Exception("no venue"); }
              if(email==null) { throw new Exception("no email"); }

                var eventNode = _contentService.GetById(nodeId);
                var venues = _umbracoHelper.Content(1059).Siblings().FirstOrDefault(m => m.IsDocumentType(Venues.ModelTypeAlias)).DescendantsOfType(Venue.ModelTypeAlias).Select(m => new Venue(m, _ipvfb));
                var venueMatch = venues.FirstOrDefault(m => m.Name + ", " + m.City == venue);

                organizer = "";
                var currentMember = await _memberManager.GetCurrentMemberAsync();
                if (currentMember != null)
                {
                    organizer = currentMember.Id;
                }
                if (poster != null && poster.Length > 0)
                {
                    IMedia media = _mediaService.CreateMedia(poster.FileName, Constants.System.Root, Constants.Conventions.MediaTypes.Image);
                    using (Stream stream = poster.OpenReadStream())
                    {

                        media.SetValue("umbracoFile", stream);
                        media.SetValue(_mediaFileManager, _mediaUrlGeneratorCollection, _shortStringHelper, _contentTypeBaseServiceProvider, Constants.Conventions.Media.File, poster.FileName, stream);
                        _mediaService.Save(media);
                    }

                    eventNode.SetValue("poster", media.GetUdi());
                }

                string tagsToSave = "";
                if (!tags.IsNullOrWhiteSpace())
                {
                    var splitTags = tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(m => Udi.Create(Constants.UdiEntityType.Document, new Guid(m)).UriValue.ToString()).ToList();
                    tagsToSave = splitTags.Aggregate((a, b) => a + "," + b);
                }

                eventNode.Name = startDate.ToString("yyyy-MM-dd") + " " + acts;
                eventNode.SetValue("startDate", startDate);
                eventNode.SetValue("endDate", endDate);
                eventNode.SetValue("acts", acts);
                eventNode.SetValue("email", email);

                if (venueMatch != null)
                {
                    eventNode.SetValue("venues", Udi.Create(Constants.UdiEntityType.Document, venueMatch.Key));
                }
                else
                {
                    eventNode.SetValue("venues", null);
                }
                eventNode.SetValue("venue", venue);
                eventNode.SetValue("description", description);
                eventNode.SetValue("link", link);
                eventNode.SetValue("organizer", organizer);
                eventNode.SetValue("tags", tagsToSave);
                eventNode.SetValue("status", JsonConvert.SerializeObject(new[] { status }));


                _contentService.SaveAndPublish(eventNode);

                TempData["SuccessMessage"] = eventNode.Name + " edited successfully.";

                return RedirectToCurrentUmbracoPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error editing event. Please try again." + ex.Message;

                return CurrentUmbracoPage();
            }
        }


    }
    }
