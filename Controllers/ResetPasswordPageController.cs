using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace website.Controllers
{
    public class ResetPasswordPageController : RenderController
    {
        private readonly MemberPasswordConfigurationSettings _pwSettings;

        public ResetPasswordPageController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IOptions<MemberPasswordConfigurationSettings> pwSettings)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _pwSettings = pwSettings.Value;
        }

        public override IActionResult Index()
        {
            ViewBag.RequiredLength = _pwSettings.RequiredLength;
            ViewBag.RequireDigit = _pwSettings.RequireDigit;
            ViewBag.RequireLowercase = _pwSettings.RequireLowercase;
            ViewBag.RequireUppercase = _pwSettings.RequireUppercase;
            ViewBag.RequireNonLetterOrDigit = _pwSettings.RequireNonLetterOrDigit;

            return CurrentTemplate(CurrentPage!);
        }
    }
}
