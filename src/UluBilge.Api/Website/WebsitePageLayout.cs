using System.Net;

namespace UluBilge.Api.Website;

internal static class WebsitePageLayout
{
    private const string BaseUrl = "https://ulubilge.uretimsistemi.com";

    public static string Render(string path, PageLanguage language, string title, string description, string content)
    {
        return $$"""
            <!doctype html>
            <html lang="{{language.Code}}">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{WebUtility.HtmlEncode(title)}} · Ulu Bilge</title>
                <meta name="description" content="{{WebUtility.HtmlEncode(description)}}">
                <meta name="theme-color" content="#2944ee">
                <link rel="icon" href="/favicon.ico">
                <link rel="canonical" href="{{BaseUrl}}{{language.Link(path)}}">
                <link rel="alternate" hreflang="tr" href="{{BaseUrl}}{{path}}?lang=tr">
                <link rel="alternate" hreflang="en" href="{{BaseUrl}}{{path}}?lang=en">
                <link rel="alternate" hreflang="x-default" href="{{BaseUrl}}{{path}}">
                <link rel="stylesheet" href="/css/website.css">
            </head>
            <body>
                <a class="skip-link" href="#main">{{language.Text("İçeriğe geç", "Skip to content")}}</a>
                <div class="page">
                    <header class="masthead">
                        <a class="wordmark" href="{{language.Link("/")}}" aria-label="{{language.Text("Ulu Bilge ana sayfa", "Ulu Bilge home")}}">Ulu Bilge<span aria-hidden="true">.</span></a>
                        <nav class="main-nav" aria-label="{{language.Text("Ana menü", "Main navigation")}}">
                            <a href="{{language.Link("/")}}"{{(path == "/" ? " aria-current=\"page\"" : "")}}>{{language.Text("Ana sayfa", "Home")}}</a>
                            <a href="{{language.Link("/support")}}"{{(path == "/support" ? " aria-current=\"page\"" : "")}}>{{language.Text("Destek", "Support")}}</a>
                        </nav>
                        {{language.Switcher(path)}}
                    </header>
                    <main id="main">{{content}}</main>
                    {{language.Footer()}}
                </div>
            </body>
            </html>
            """;
    }
}
