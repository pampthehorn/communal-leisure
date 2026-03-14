using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.PublishedModels;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Extensions;
using website.Models.ViewModels;
using website.Services;

namespace website.Controllers
{
    public class RegistrationSurfaceController : SurfaceController
    {
        private readonly IMemberManager _memberManager;
        private readonly IMemberService _memberService;
        private readonly IEmailService _emailService;
        private readonly IRazorViewToStringRenderer _viewRenderer;
        private readonly UmbracoHelper _umbracoHelper;
        private readonly IPublishedValueFallback _ipvfb;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RegistrationSurfaceController> _logger;

        public RegistrationSurfaceController(
            IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedUrlProvider publishedUrlProvider,
            IMemberManager memberManager,
            IMemberService memberService,
            IEmailService emailService,
            IRazorViewToStringRenderer viewRenderer,
            UmbracoHelper umbracoHelper,
            IPublishedValueFallback ipvfb,
            IConfiguration configuration,
            ILogger<RegistrationSurfaceController> logger)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _memberManager = memberManager;
            _memberService = memberService;
            _emailService = emailService;
            _viewRenderer = viewRenderer;
            _umbracoHelper = umbracoHelper;
            _ipvfb = ipvfb;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HandleRegister(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return CurrentUmbracoPage();

            var existingMember = await _memberManager.FindByEmailAsync(model.Email);
            if (existingMember != null)
            {
                ModelState.AddModelError(string.Empty, "An account with this email address already exists.");
                return CurrentUmbracoPage();
            }

            try
            {
                var memberTypeAlias = "Member";
                var member = _memberService.CreateMemberWithIdentity(
                    model.Email,
                    model.Email,
                    model.Name,
                    memberTypeAlias);

                member.IsApproved = false;
                member.SetValue("emailVerified", false);
                _memberService.Save(member);

                var identityUser = await _memberManager.FindByEmailAsync(model.Email);
                if (identityUser == null)
                {
                    ModelState.AddModelError(string.Empty, "Registration failed. Please try again.");
                    return CurrentUmbracoPage();
                }

                var passwordResult = await _memberManager.AddPasswordAsync(identityUser, model.Password);
                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return CurrentUmbracoPage();
                }

                var token = await _memberManager.GenerateEmailConfirmationTokenAsync(identityUser);

                var registerPage = _umbracoHelper.ContentAtRoot()
                    .DescendantsOrSelfOfType("register")
                    .FirstOrDefault();

                if (registerPage == null)
                {
                    _logger.LogWarning("Could not find register page content node — verification email not sent for {Email}", model.Email);
                }
                else
                {
                    var verifyLink = registerPage.Url(mode: UrlMode.Absolute)
                        + $"?verify=true&token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(model.Email)}";

                    var emailBody = await _viewRenderer.RenderViewToStringAsync(
                        "~/Views/Emails/VerifyEmail.cshtml", verifyLink);

                    await _emailService.SendEmailAsync(model.Email, "Verify your email address", emailBody);
                }

                TempData["RegisterStatus"] = "registered";
                return RedirectToCurrentUmbracoPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Registration failed. Please try again.");
                return CurrentUmbracoPage();
            }
        }

        [HttpGet]
        public async Task<IActionResult> ApproveMember(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return Content("Invalid approval link.");

            var expectedToken = GenerateApprovalToken(email);
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token),
                Encoding.UTF8.GetBytes(expectedToken)))
            {
                return Content("Invalid approval link.");
            }

            var member = _memberService.GetByEmail(email);
            if (member == null)
                return Content("Member not found.");

            if (member.IsApproved)
                return Content("This member has already been approved.");

            member.IsApproved = true;
            _memberService.Save(member);

            var loginPage = _umbracoHelper.ContentAtRoot()
                .DescendantsOrSelfOfType("login")
                .FirstOrDefault();

            var loginUrl = loginPage?.Url(mode: UrlMode.Absolute) ?? "/";

            var emailBody = await _viewRenderer.RenderViewToStringAsync(
                "~/Views/Emails/MemberApproved.cshtml", loginUrl);

            await _emailService.SendEmailAsync(email, "Your account has been approved", emailBody);

            return Content($"<html><body><h2>Member Approved</h2><p>{System.Net.WebUtility.HtmlEncode(member.Name)} ({System.Net.WebUtility.HtmlEncode(email)}) has been approved and notified by email.</p></body></html>", "text/html");
        }

        public static string GenerateApprovalToken(string email)
        {
            var secret = Environment.GetEnvironmentVariable("APPROVAL_TOKEN_SECRET")
                ?? "communal-leisure-approval-key";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
            return Convert.ToBase64String(hash);
        }
    }
}
