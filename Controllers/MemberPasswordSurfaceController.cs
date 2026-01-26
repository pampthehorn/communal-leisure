using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Extensions;
using website.Models;  
using website.Models.ViewModels;
using website.Services;

namespace website.Controllers
{
    public class MemberPasswordSurfaceController : SurfaceController
    {
        private readonly IMemberManager _memberManager;
        private readonly IEmailService _emailService;
        private readonly IPublishedContentQuery _publishedContentQuery;

        public MemberPasswordSurfaceController(
            IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedUrlProvider publishedUrlProvider,
            IMemberManager memberManager,
            IEmailService emailService,
            IPublishedContentQuery publishedContentQuery)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _memberManager = memberManager;
            _emailService = emailService;
            _publishedContentQuery = publishedContentQuery;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HandleForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return CurrentUmbracoPage();
            }

            var member = await _memberManager.FindByEmailAsync(model.Email);
            if (member != null)
            {
                var token = await _memberManager.GeneratePasswordResetTokenAsync(member);
                
                var resetPage = _publishedContentQuery.ContentAtRoot()
                                     .FirstOrDefault(x => x.ContentType.Alias == "forgotPasswordPage").Children()
                                .FirstOrDefault(x => x.ContentType.Alias == "resetPasswordPage");

                if (resetPage != null)
                {
                    var resetLink = resetPage.Url(mode: UrlMode.Absolute)
                                    + $"?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(model.Email)}";

                    var subject = "Password Reset Request";
                    var body = $"<p>Please reset your password by clicking here: <a href='{resetLink}'>{resetLink}</a></p>";

                    await _emailService.SendEmailAsync(model.Email, subject, body);
                }
                else
                {
                   
                }
            }

            TempData["Status"] = "If an account with that email exists, a password reset link has been sent.";
            return RedirectToCurrentUmbracoPage();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HandleResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return CurrentUmbracoPage();
            }

            var member = await _memberManager.FindByEmailAsync(model.Email);
            if (member == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid request.");
                return CurrentUmbracoPage();
            }

            var result = await _memberManager.ResetPasswordAsync(member, model.Token, model.Password);
            if (result.Succeeded)
            {
                TempData["Status"] = "Password reset successfully. You can now log in.";
                return Redirect("/");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return CurrentUmbracoPage();
        }
    }
}