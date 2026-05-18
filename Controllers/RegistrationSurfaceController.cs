using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models;
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
        private const string PromoterRoleName = "promoter";

        private readonly IMemberManager _memberManager;
        private readonly IMemberService _memberService;
        private readonly IEmailService _emailService;
        private readonly IRazorViewToStringRenderer _viewRenderer;
        private readonly UmbracoHelper _umbracoHelper;
        private readonly IPublishedValueFallback _ipvfb;
        private readonly IConfiguration _configuration;
        private readonly ITurnstileService _turnstileService;
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
            ITurnstileService turnstileService,
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
            _turnstileService = turnstileService;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HandleRegister(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return CurrentUmbracoPage();

            var turnstileToken = Request.Form["cf-turnstile-response"].ToString();
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (!await _turnstileService.VerifyAsync(turnstileToken, remoteIp))
            {
                ModelState.AddModelError(string.Empty, "Please complete the verification challenge and try again.");
                return CurrentUmbracoPage();
            }

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
                member.SetValue("wantsToBeAPromoter", model.WantsToBePromoter);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPromoter()
        {
            var current = await _memberManager.GetCurrentMemberAsync();
            if (current == null)
            {
                return Forbid();
            }

            var member = _memberService.GetByEmail(current.Email!);
            if (member == null)
            {
                return NotFound();
            }

            var roles = _memberService.GetAllRoles(member.Id) ?? Enumerable.Empty<string>();
            if (roles.Contains(PromoterRoleName))
            {
                TempData["PromoterRequestStatus"] = "alreadyApproved";
                return RedirectToCurrentUmbracoPage();
            }

            member.SetValue("wantsToBeAPromoter", true);
            _memberService.Save(member);

            try
            {
                await SendPromoterRequestEmailAsync(
                    member, _umbracoHelper, _ipvfb, _viewRenderer, _emailService,
                    Request.Scheme, Request.Host.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send promoter request email for {Email}", member.Email);
            }

            TempData["PromoterRequestStatus"] = "requested";
            return RedirectToCurrentUmbracoPage();
        }

        [HttpGet]
        public IActionResult ConfirmApprovePromoter(string email, string token)
        {
            if (!IsValidApprovalRequest(email, token))
                return Content("Invalid approval link.");

            var member = _memberService.GetByEmail(email);
            if (member == null)
                return Content("Member not found.");

            var existingRoles = _memberService.GetAllRoles(member.Id) ?? Enumerable.Empty<string>();
            if (existingRoles.Contains(PromoterRoleName))
            {
                return Content($"<html><body><h2>Already a Promoter</h2><p>{System.Net.WebUtility.HtmlEncode(member.Name)} ({System.Net.WebUtility.HtmlEncode(email)}) already has the promoter role.</p></body></html>", "text/html");
            }

            var postUrl = $"{Request.Scheme}://{Request.Host}/umbraco/surface/RegistrationSurface/ApprovePromoter";
            var html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<title>Approve promoter</title>
<style>
body {{ font-family: Arial, sans-serif; line-height: 1.6; max-width: 560px; margin: 40px auto; padding: 0 16px; }}
.approve-btn {{ display: inline-block; padding: 12px 24px; background: #000; color: #fff; text-decoration: none; font-weight: bold; border: 0; cursor: pointer; font-size: 16px; }}
</style>
</head>
<body>
<h2>Approve promoter request</h2>
<p>Approve <strong>{System.Net.WebUtility.HtmlEncode(member.Name)}</strong> ({System.Net.WebUtility.HtmlEncode(email)}) as a promoter? They'll be added to the promoter group and notified by email.</p>
<form action=""{System.Net.WebUtility.HtmlEncode(postUrl)}"" method=""post"" style=""margin: 24px 0;"">
<input type=""hidden"" name=""email"" value=""{System.Net.WebUtility.HtmlEncode(email)}"" />
<input type=""hidden"" name=""token"" value=""{System.Net.WebUtility.HtmlEncode(token)}"" />
<button type=""submit"" class=""approve-btn"">Yes, approve as promoter</button>
</form>
</body>
</html>";
            return Content(html, "text/html");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult ApprovePromoter(string email, string token)
        {
            if (!IsValidApprovalRequest(email, token))
                return Content("Invalid approval link.");

            var member = _memberService.GetByEmail(email);
            if (member == null)
                return Content("Member not found.");

            var existingRoles = _memberService.GetAllRoles(member.Id) ?? Enumerable.Empty<string>();
            if (existingRoles.Contains(PromoterRoleName))
            {
                return Content($"<html><body><h2>Already a Promoter</h2><p>{System.Net.WebUtility.HtmlEncode(member.Name)} ({System.Net.WebUtility.HtmlEncode(email)}) already has the promoter role.</p></body></html>", "text/html");
            }

            _memberService.AssignRoles(new[] { member.Id }, new[] { PromoterRoleName });

            try
            {
                var loginPage = _umbracoHelper.ContentAtRoot()
                    .DescendantsOrSelfOfType("login")
                    .FirstOrDefault();

                var loginUrl = loginPage?.Url(mode: UrlMode.Absolute) ?? "/";

                var emailBody = _viewRenderer.RenderViewToStringAsync(
                    "~/Views/Emails/PromoterApproved.cshtml", loginUrl).GetAwaiter().GetResult();

                _emailService.SendEmailAsync(email, "You're now a Communal Leisure promoter", emailBody)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send promoter-approved confirmation to {Email}", email);
            }

            return Content($"<html><body><h2>Promoter Approved</h2><p>{System.Net.WebUtility.HtmlEncode(member.Name)} ({System.Net.WebUtility.HtmlEncode(email)}) has been added to the promoter group and notified by email.</p></body></html>", "text/html");
        }

        private static bool IsValidApprovalRequest(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return false;

            var expectedToken = GeneratePromoterApprovalToken(email);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token),
                Encoding.UTF8.GetBytes(expectedToken));
        }

        public static string GeneratePromoterApprovalToken(string email)
        {
            var secret = Environment.GetEnvironmentVariable("APPROVAL_TOKEN_SECRET")
                ?? "communal-leisure-approval-key";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes("promoter:" + email.ToLowerInvariant()));
            return Convert.ToBase64String(hash);
        }

        public static async Task SendPromoterRequestEmailAsync(
            IMember member,
            UmbracoHelper umbracoHelper,
            IPublishedValueFallback ipvfb,
            IRazorViewToStringRenderer viewRenderer,
            IEmailService emailService,
            string scheme,
            string host)
        {
            var eventsNode = umbracoHelper.ContentAtRoot()
                .FirstOrDefault(m => m.ContentType.Alias == "events");
            if (eventsNode == null) return;

            var events = new Events(eventsNode, ipvfb);
            var token = GeneratePromoterApprovalToken(member.Email);
            var approveUrl = $"{scheme}://{host}/umbraco/surface/RegistrationSurface/ConfirmApprovePromoter"
                + $"?email={Uri.EscapeDataString(member.Email)}&token={Uri.EscapeDataString(token)}";

            var model = new PromoterApprovalEmailModel
            {
                MemberName = member.Name ?? member.Email,
                MemberEmail = member.Email,
                ApproveUrl = approveUrl,
            };

            var body = await viewRenderer.RenderViewToStringAsync("~/Views/Emails/PromoterApproval.cshtml", model);
            var notifyEmails = events.NotifyEmails?.ToList() ?? new List<string>();

            await emailService.SendEmailAsync(
                "comlesweb@gmail.com",
                $"Promoter request: {model.MemberName}",
                body,
                notifyEmails);
        }
    }

    public class PromoterApprovalEmailModel
    {
        public string MemberName { get; set; } = string.Empty;
        public string MemberEmail { get; set; } = string.Empty;
        public string ApproveUrl { get; set; } = string.Empty;
    }
}
