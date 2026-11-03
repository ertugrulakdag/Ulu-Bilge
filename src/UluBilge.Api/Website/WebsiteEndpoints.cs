using System.Text;
using Microsoft.Extensions.Options;

namespace UluBilge.Api.Website;

public static class WebsiteEndpoints
{
    public static WebApplication MapWebsitePages(this WebApplication app)
    {
        foreach (var path in new[] { "/", "/support", "/privacy", "/terms" })
        {
            app.MapGet(path, (HttpContext context, IOptions<WebsiteOptions> options) =>
            {
                var language = PageLanguage.FromRequest(context);
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'self'; img-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
                context.Response.Headers.XContentTypeOptions = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                return Results.Content(WebsitePages.Render(path, language, options.Value), "text/html; charset=utf-8", Encoding.UTF8);
            }).AllowAnonymous().ExcludeFromDescription();
        }
        return app;
    }
}
