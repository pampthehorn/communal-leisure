using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Services;

namespace website.Composers
{
    // public class SetExistingMembersEmailVerifiedComposer : IComposer
    // {
    //     public void Compose(IUmbracoBuilder builder)
    //     {
    //         builder.AddNotificationAsyncHandler<Umbraco.Cms.Core.Notifications.UmbracoApplicationStartedNotification,
    //             SetExistingMembersEmailVerifiedHandler>();
    //     }
    // }

    public class SetExistingMembersEmailVerifiedHandler
        : Umbraco.Cms.Core.Events.INotificationAsyncHandler<Umbraco.Cms.Core.Notifications.UmbracoApplicationStartedNotification>
    {
        private readonly IMemberService _memberService;
        private readonly IWebHostEnvironment _hostEnvironment;

        public SetExistingMembersEmailVerifiedHandler(IMemberService memberService, IWebHostEnvironment hostEnvironment)
        {
            _memberService = memberService;
            _hostEnvironment = hostEnvironment;
        }

        public Task HandleAsync(
            Umbraco.Cms.Core.Notifications.UmbracoApplicationStartedNotification notification,
            CancellationToken cancellationToken)
        {
            var flagPath = Path.Combine(_hostEnvironment.ContentRootPath, "umbraco", "models", "emailVerifiedMigration.flag");

            if (File.Exists(flagPath))
                return Task.CompletedTask;

            long totalMembers;
            var members = _memberService.GetAll(0, int.MaxValue, out totalMembers);

            foreach (var member in members)
            {
                var current = member.GetValue<bool>("emailVerified");
                if (!current)
                {
                    member.SetValue("emailVerified", true);
                    _memberService.Save(member);
                }
            }

            File.WriteAllText(flagPath, $"Migration completed at {DateTime.UtcNow:O}");

            return Task.CompletedTask;
        }
    }
}
