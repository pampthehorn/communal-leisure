using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Web.Website.Controllers;

namespace website.Controllers
{
    public class MemberLoginSurfaceController : SurfaceController
    {
        private readonly IMemberManager _memberManager;
        private readonly MemberSignInManager _memberSignInManager;

        public MemberLoginSurfaceController(
            IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedUrlProvider publishedUrlProvider,
            IMemberManager memberManager,
            MemberSignInManager memberSignInManager)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _memberManager = memberManager;
            _memberSignInManager = memberSignInManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HandleLogin(string username, string password, bool rememberMe = false, string? redirectUrl = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["LoginError"] = "Please enter your username/email and password.";
                return RedirectToCurrentUmbracoPage();
            }

            // Try signing in with the input as-is (username)
            var result = await _memberSignInManager.PasswordSignInAsync(username, password, rememberMe, true);

            // If that failed, try looking up by email
            if (!result.Succeeded)
            {
                var member = await _memberManager.FindByEmailAsync(username);
                if (member != null && member.UserName != null)
                {
                    result = await _memberSignInManager.PasswordSignInAsync(member.UserName, password, rememberMe, true);
                }
            }

            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(redirectUrl) && Url.IsLocalUrl(redirectUrl))
                {
                    return Redirect(redirectUrl);
                }
                return Redirect("/login");
            }

            if (result.IsLockedOut)
            {
                TempData["LoginError"] = "Account is locked. Please try again later.";
            }
            else
            {
                TempData["LoginError"] = "Invalid username/email or password.";
            }

            return RedirectToCurrentUmbracoPage();
        }
    }
}
