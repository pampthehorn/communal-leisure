using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;

namespace website.Controllers
{

	public class PosterDetailController : UmbracoApiController
	{
		private readonly Serilog.ILogger _logger;
		private IConfiguration _config;
        private readonly IMediaService _mediaService;
        private readonly MediaFileManager _mediaFileManager;
        private readonly MediaUrlGeneratorCollection _mediaUrlGeneratorCollection;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;

        public PosterDetailController(Serilog.ILogger logger, IConfiguration config, IMediaService mediaService, MediaFileManager mediaFileManager, MediaUrlGeneratorCollection mediaUrlGeneratorCollection, IShortStringHelper shortStringHelper, IContentTypeBaseServiceProvider contentTypeBaseServiceProvider)
        {
			_logger = logger;
			_config = config;
            _mediaService = mediaService;
            _mediaFileManager = mediaFileManager;
            _mediaUrlGeneratorCollection = mediaUrlGeneratorCollection;
            _shortStringHelper = shortStringHelper;
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        }


        [HttpPost]
        public IActionResult UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            IMedia media = _mediaService.CreateMedia("Unicorn", Constants.System.Root, Constants.Conventions.MediaTypes.Image);
            using (Stream stream = file.OpenReadStream())
            {

                media.SetValue("umbracoFile", stream);
                media.SetValue(_mediaFileManager, _mediaUrlGeneratorCollection, _shortStringHelper, _contentTypeBaseServiceProvider, Constants.Conventions.Media.File, file.FileName, stream);
                _mediaService.Save(media);
            }
            return Ok(new { mediaId = media.Id });
        }
    }






}
