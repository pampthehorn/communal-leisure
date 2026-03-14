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

if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value;
        if (path != null && path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
        {
            var localPath = Path.Combine(app.Environment.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(localPath))
            {
                var liveUrl = $"https://communalleisure.com{path}{context.Request.QueryString}";
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(liveUrl);
                if (response.IsSuccessStatusCode)
                {
                    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                    await response.Content.CopyToAsync(context.Response.Body);
                    return;
                }
            }
        }
        await next();
    });
}

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
