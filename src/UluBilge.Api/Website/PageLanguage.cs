using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace UluBilge.Api.Website;

internal sealed record PageLanguage(string Code)
{
    private const string CookieName = "UluBilge.Language";

    public string Text(string turkish, string english) => Code == "en" ? english : turkish;

    public string Link(string path) => QueryHelpers.AddQueryString(path, "lang", Code);

    public static PageLanguage FromRequest(HttpContext context, string? submittedLanguage = null)
    {
        var explicitLanguage = Supported(submittedLanguage ?? context.Request.Query["lang"].ToString());
        var code = explicitLanguage ?? Supported(context.Request.Cookies[CookieName]);
        if (code is null)
        {
            code = context.Request.GetTypedHeaders().AcceptLanguage?
                .Where(value => value.Quality.GetValueOrDefault(1) > 0)
                .OrderByDescending(value => value.Quality.GetValueOrDefault(1))
                .Select(value => Supported(value.Value.ToString().Split('-')[0]))
                .FirstOrDefault(value => value is not null) ?? "tr";
        }

        if (explicitLanguage is not null)
        {
            context.Response.Cookies.Append(CookieName, code, new CookieOptions
            {
                HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax,
                IsEssential = true, MaxAge = TimeSpan.FromDays(365), Path = "/"
            });
        }
        context.Response.Headers.ContentLanguage = code;
        context.Response.Headers.Vary = "Accept-Language, Cookie";
        return new(code);
    }

    public string Switcher(string path, IReadOnlyDictionary<string, string>? parameters = null)
    {
        string Item(string code, string label)
        {
            var values = parameters?.ToDictionary(pair => pair.Key, pair => (string?)pair.Value) ?? [];
            values["lang"] = code;
            var href = WebUtility.HtmlEncode(QueryHelpers.AddQueryString(path, values));
            return $"<a href=\"{href}\" lang=\"{code}\" hreflang=\"{code}\"{(Code == code ? " aria-current=\"true\"" : "")}>{label}</a>";
        }
        return $"<nav class=\"language-picker\" aria-label=\"{Text("Dil seçimi", "Language selection")}\">{Item("tr", "Türkçe")}{Item("en", "English")}</nav>";
    }

    public string Footer() => $$"""
        <footer class="site-footer">
            <span>Ulu Bilge · {{Text("Raporlarınız, izninizle.", "Your reports, with your permission.")}}</span>
            <nav aria-label="{{Text("Bilgi sayfaları", "Information pages")}}">
                <a href="{{Link("/support")}}">{{Text("Destek", "Support")}}</a>
                <a href="{{Link("/privacy")}}">{{Text("Gizlilik", "Privacy")}}</a>
                <a href="{{Link("/terms")}}">{{Text("Kullanım koşulları", "Terms of service")}}</a>
            </nav>
        </footer>
        """;

    private static string? Supported(string? code) => code?.ToLowerInvariant() switch
    {
        "tr" => "tr", "en" => "en", _ => null
    };
}
