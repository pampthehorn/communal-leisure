export const onInit = (host, extensionRegistry) => {
    extensionRegistry.unregister("Umb.Dashboard.SettingsWelcome");
    extensionRegistry.unregister("Umb.Dashboard.UmbracoNews");

    const target = "/umbraco/section/content/workspace/document/edit/888093bf-4065-4e61-bcb9-77eefef477bd/invariant";
    const path = window.location.pathname.replace(/\/+$/, "");
    if (path === "/umbraco") {
        window.location.replace(target);
    }
};
