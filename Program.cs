using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Rewrite;
using website.Controllers;
using website.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.Services.AddCors(options =>
{
    options.AddPolicy("MyCorsPolicy", builder =>
    {
        builder.WithOrigins("https://localhost","http://com.les","https://communalleisure.com", "https://www.communalleisure.com")
               .AllowAnyHeader()
               .AllowAnyMethod();
    });

});

builder.Services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddScoped<IOrderProcessingService, OrderProcessingService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(60);
    options.SlidingExpiration = true;
});

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();

var forwardedHeaderOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaderOptions.KnownNetworks.Clear(); 
forwardedHeaderOptions.KnownProxies.Clear(); 
app.UseForwardedHeaders(forwardedHeaderOptions);
app.UseCors("MyCorsPolicy");

var options = new RewriteOptions().AddIISUrlRewrite(app.Environment.WebRootFileProvider, "rules/UrlRules.xml");

app.UseRewriter(options);

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        

    })
    .WithEndpoints(u =>
    {
       // u.UseInstallerEndpoints();
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
