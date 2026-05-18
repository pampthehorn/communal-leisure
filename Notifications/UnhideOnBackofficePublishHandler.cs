using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace website.Notifications;

public class UnhideOnBackofficePublishHandler : INotificationHandler<ContentPublishingNotification>
{
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IContentService _contentService;

    public UnhideOnBackofficePublishHandler(
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IContentService contentService)
    {
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _contentService = contentService;
    }

    public void Handle(ContentPublishingNotification notification)
    {
        if (_backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser == null)
        {
            return;
        }

        foreach (var content in notification.PublishedEntities)
        {
            if (content.ContentType.Alias != "event")
            {
                continue;
            }

            if (content.GetValue<bool>("hide") == false)
            {
                continue;
            }

            content.SetValue("hide", false);
            _contentService.Save(content);
        }
    }
}
