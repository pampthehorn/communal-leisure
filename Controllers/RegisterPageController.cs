using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.PublishedModels;
using Umbraco.Extensions;
using website.Services;

namespace website.Controllers
{
    public class RegisterController : RenderController
    {
        private readonly MemberPasswordConfigurationSettings _pwSettings;
        private readonly IMemberManager _memberManager;
        private readonly IMemberService _memberService;
        private readonly IEmailService _emailService;
        private readonly IRazorViewToStringRenderer _viewRenderer;
        private readonly UmbracoHelper _umbracoHelper;
        private readonly IPublishedValueFallback _ipvfb;
        private readonly ILogger<RegisterController> _logger;

        public RegisterController(
            ILogger<RegisterController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IOptions<MemberPasswordConfigurationSettings> pwSettings,
            IMemberManager memberManager,
            IMemberService memberService,
            IEmailService emailService,
            IRazorViewToStringRenderer viewRenderer,
            UmbracoHelper umbracoHelper,
            IPublishedValueFallback ipvfb)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _pwSettings = pwSettings.Value;
            _memberManager = memberManager;
            _memberService = memberService;
            _emailService = emailService;
            _viewRenderer = viewRenderer;
            _umbracoHelper = umbracoHelper;
            _ipvfb = ipvfb;
            _logger = logger;
        }

        public override IActionResult Index()
        {
            ViewBag.RequiredLength = _pwSettings.RequiredLength;
            ViewBag.RequireDigit = _pwSettings.RequireDigit;
            ViewBag.RequireLowercase = _pwSettings.RequireLowercase;
            ViewBag.RequireUppercase = _pwSettings.RequireUppercase;
            ViewBag.RequireNonLetterOrDigit = _pwSettings.RequireNonLetterOrDigit;

            var verify = Request.Query["verify"].ToString();
            var token = Request.Query["token"].ToString();
            var email = Request.Query["email"].ToString();

            if (verify == "true" && !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(email))
            {
                var task = ProcessEmailVerification(email, token);
                task.Wait();
            }

            return CurrentTemplate(CurrentPage!);
        }

        private async Task ProcessEmailVerification(string email, string token)
        {
            try
            {
                var identityUser = await _memberManager.FindByEmailAsync(email);
                if (identityUser == null)
                {
                    ViewBag.VerifyResult = "error";
                    ViewBag.VerifyMessage = "Invalid verification link.";
                    return;
                }

                var result = await _memberManager.ConfirmEmailAsync(identityUser, token);
                if (!result.Succeeded)
                {
                    ViewBag.VerifyResult = "error";
                    ViewBag.VerifyMessage = "This verification link is invalid or has already been used.";
                    return;
                }

                var member = _memberService.GetByEmail(email);
                if (member != null)
                {
                    member.SetValue("emailVerified", true);
                    member.IsApproved = true;
                    _memberService.Save(member);

                    if (member.GetValue<bool>("wantsToBeAPromoter"))
                    {
                        await RegistrationSurfaceController.SendPromoterRequestEmailAsync(
                            member, _umbracoHelper, _ipvfb, _viewRenderer, _emailService,
                            Request.Scheme, Request.Host.ToString());
                    }
                }

                ViewBag.VerifyResult = "success";
                ViewBag.VerifyMessage = member != null && member.GetValue<bool>("wantsToBeAPromoter")
                    ? "Your email has been verified. We've passed your promoter request to the admins — you'll hear back soon. In the meantime you can log in and browse."
                    : "Your email has been verified. You can now log in.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email verification failed for {Email}", email);
                ViewBag.VerifyResult = "error";
                ViewBag.VerifyMessage = "Verification failed. Please try again or contact us.";
            }
        }
    }

}
