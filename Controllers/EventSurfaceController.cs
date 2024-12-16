namespace website.Controllers
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using System;
    using System.IO;
    using Umbraco.Cms.Core.Services;
    using Umbraco.Extensions;
    using Umbraco.Cms.Web.Website.Controllers;
    using Umbraco.Cms.Core.IO;
    using Umbraco.Cms.Core.Models;
    using Umbraco.Cms.Core.PropertyEditors;
    using Umbraco.Cms.Core.Strings;
    using Umbraco.Cms.Core;
    using Umbraco.Cms.Core.Web;
    using Umbraco.Cms.Web.Common.PublishedModels;
    using Umbraco.Cms.Core.Cache;
    using Umbraco.Cms.Core.Logging;
    using Umbraco.Cms.Core.Routing;
    using Umbraco.Cms.Infrastructure.Persistence;
    using Umbraco.Cms.Core.Security;
    using System.Threading.Tasks;
    using System.Net.Mail;
    using System.Net;

    using Umbraco.Cms.Web.Common;
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Newtonsoft.Json;

    using Udi = Umbraco.Cms.Core.Udi;

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



        public EventSurfaceController(IContentService contentService, IMediaService mediaService, MediaFileManager mediaFileManager, MediaUrlGeneratorCollection mediaUrlGeneratorCollection, IShortStringHelper shortStringHelper, IContentTypeBaseServiceProvider contentTypeBaseServiceProvider, IUmbracoContextAccessor umbracoContextAccessor,
        IUmbracoDatabaseFactory databaseFactory,
        ServiceContext services,
        AppCaches appCaches,
        IProfilingLogger profilingLogger,
        IPublishedUrlProvider publishedUrlProvider,
        IMemberManager memberManager,
        IConfiguration configuration,
        UmbracoHelper umbracoHelper,
        IPublishedValueFallback ipvfb)
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
        }

            [HttpPost]
            public async Task<IActionResult> SubmitEventAsync(IFormFile poster, DateTime startDate, DateTime endDate, string acts, string venue, string description, string link, string organizer, string tags, string email)
        {
            try
                {
                var venues = _umbracoHelper.Content(1059).Siblings().FirstOrDefault(m => m.IsDocumentType(Venues.ModelTypeAlias)).DescendantsOfType(Venue.ModelTypeAlias).Select(m => new Venue(m, _ipvfb));
                var venueMatch = venues.FirstOrDefault(m => m.Name + ", " + m.City == venue);
        
                var eventNode = _contentService.Create(acts,1059, Event.ModelTypeAlias ,-1);
                    organizer = "";
                    var currentMember = await _memberManager.GetCurrentMemberAsync();
                    if (currentMember != null) {
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

                MailMessage mailMessage = new MailMessage();

                    if(eventsNode.NotifyEmails!=null)
                    foreach(var address in eventsNode.NotifyEmails)
                    {
                        var ma = new MailAddress(address, address);

                        mailMessage.Bcc.Add(ma);
                    }
                    var toMailAddress = new MailAddress("comlesweb@gmail.com", $"comlesweb");
                    mailMessage.To.Add(toMailAddress);

                    string host = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:Host");
                    int port = _configuration.GetValue<int>("Umbraco:CMS:Global:Smtp:Port");
                    string fromAddress = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:From");
                    string userName = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:Username");
                    string password = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:Password");

                    mailMessage.Body = acts + " " + startDate + editLinkText;
                    mailMessage.From = new MailAddress(fromAddress);
                    mailMessage.IsBodyHtml = true;
                    mailMessage.Subject = "new event: " + acts + " " + startDate;

                    using (SmtpClient smtp = new SmtpClient())
                    {
                        smtp.Host = host;
                        NetworkCredential NetworkCred = new NetworkCredential(userName, password);
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = NetworkCred;
                        smtp.Port = port;
                        smtp.Send(mailMessage);
                    }
                
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
                TempData["ErrorMessage"] = "Error editing event. Please try again.";

                return CurrentUmbracoPage();
            }
        }


    }
    }
