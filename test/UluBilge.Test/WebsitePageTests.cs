using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UluBilge.Api.Controllers;
using UluBilge.Api.Services;
using UluBilge.App.Commands.AuthHandler;
using UluBilge.Core.Commands;
using UluBilge.Core.Entities;
using UluBilge.Data.Abstract;

namespace UluBilge.Test;

[TestFixture("Development")]
[TestFixture("Production")]
public sealed class WebsitePageTests(string environment)
{
    private WebApplicationFactory<AuthController> factory = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "UluBilge.slnx"))) root = root.Parent;
        factory = new WebApplicationFactory<AuthController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseContentRoot(Path.Combine(root!.FullName, "src", "UluBilge.Api"));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReadConnection"] = "Server=unused;Database=unused",
                ["ConnectionStrings:WriteConnection"] = "Server=unused;Database=unused",
                ["Jwt:Issuer"] = "WebsitePageTests", ["Jwt:Audience"] = "WebsitePageTests.Rest",
                ["Jwt:SigningKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["OAuth:ServerUrl"] = "https://ulubilge.example",
                ["Website:PublisherName"] = "Ertuğrul AKDAĞ", ["Website:SupportEmail"] = "ertugrulakdag@gmail.com"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.Remove(services.Single(service => service.ServiceType == typeof(IHostedService)
                    && service.ImplementationType == typeof(DatabaseSchemaHostedService)));
                services.AddScoped<IUnitOfWork, MemoryWork>();
                services.AddScoped<IOAuthClientRepository, MemoryClients>();
                services.AddScoped<IAsyncCommandHandler<LoginAsync.Command, LoginAsync.Result?>, RejectedLogin>();
            });
        });
    }

    [OneTimeTearDown]
    public async Task TearDown() => await factory.DisposeAsync();

    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost:7187"), AllowAutoRedirect = false
    });

    [TestCase("/", "tr", "Raporlarınız.")]
    [TestCase("/", "en", "Your reports.")]
    [TestCase("/support", "tr", "Size nasıl")]
    [TestCase("/support", "en", "How can")]
    [TestCase("/privacy", "tr", "Gizlilik")]
    [TestCase("/privacy", "en", "Privacy")]
    [TestCase("/terms", "tr", "Kullanım")]
    [TestCase("/terms", "en", "Terms")]
    public async Task Pages_AreAnonymousAndLocalized(string path, string language, string heading)
    {
        using var client = Client();
        using var response = await client.GetAsync(path + "?lang=" + language);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            Assert.That(response.Content.Headers.ContentLanguage, Does.Contain(language));
            Assert.That(html, Does.Contain($"<html lang=\"{language}\">").And.Contain(heading));
            Assert.That(html, Does.Contain($"href=\"/support?lang={language}\"").And.Contain($"href=\"/privacy?lang={language}\"").And.Contain($"href=\"/terms?lang={language}\""));
            Assert.That(response.Headers.GetValues("Content-Security-Policy").Single(), Does.Contain("frame-ancestors 'none'"));
            if (path != "/") Assert.That(html, Does.Contain("Ertuğrul AKDAĞ").And.Contain("mailto:ertugrulakdag@gmail.com"));
        });
    }

    [Test]
    public async Task Language_UsesHeaderThenCookieAndExplicitChoice()
    {
        using var client = Client();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9,tr;q=0.5");
        Assert.That(await client.GetStringAsync("/support"), Does.Contain("<html lang=\"en\">"));
        using var selected = await client.GetAsync("/privacy?lang=tr");
        Assert.That(selected.Headers.GetValues("Set-Cookie").Single(), Does.Contain("UluBilge.Language=tr").And.Contain("secure").And.Contain("httponly"));
        Assert.That(await client.GetStringAsync("/terms"), Does.Contain("<html lang=\"tr\">"));
        Assert.That(await client.GetStringAsync("/oauth/success"), Does.Contain("<html lang=\"tr\">"));
        Assert.That(await client.GetStringAsync("/?lang=en"), Does.Contain("<html lang=\"en\">"));
        Assert.That(await client.GetStringAsync("/support?lang=de"), Does.Contain("<html lang=\"en\">"));
    }

    [Test]
    public async Task DefaultAndUnsupportedLanguages_FallBackToTurkish()
    {
        using var client = Client();
        Assert.That(await client.GetStringAsync("/"), Does.Contain("<html lang=\"tr\">"));
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de,fr;q=0.8,en;q=0");
        Assert.That(await client.GetStringAsync("/support?lang=invalid"), Does.Contain("<html lang=\"tr\">"));
    }

    [Test]
    public async Task WebsiteAssetsAndSuccessPage_WorkWhileMcpRemainsProtected()
    {
        using var client = Client();
        using var css = await client.GetAsync("/css/website.css");
        Assert.That(css.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(css.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/css"));
        Assert.That(await client.GetStringAsync("/oauth/success?lang=en"), Does.Contain("Sign-in successful.").And.Contain("sample lunch menu"));
        using var mcp = await client.GetAsync("/mcp");
        Assert.That(mcp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var http = await client.GetAsync("http://localhost:5156/support");
        Assert.That(http.StatusCode, Is.EqualTo(environment == "Production" ? HttpStatusCode.BadRequest : HttpStatusCode.OK));
    }

    [TestCase("/.well-known/oauth-authorization-server")]
    [TestCase("/.well-known/openid-configuration")]
    public async Task OAuthLanguageSwitch_PreservesRequestAndNeverIncludesCredentials(string discoveryPath)
    {
        using var client = Client();
        var metadata = await client.GetFromJsonAsync<JsonElement>(discoveryPath);
        var resource = metadata.GetProperty("issuer").GetString()!.TrimEnd('/') + "/mcp";
        var url = QueryHelpers.AddQueryString("/oauth/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "website-test", ["redirect_uri"] = "http://127.0.0.1:43434/callback",
            ["response_type"] = "code", ["resource"] = resource,
            ["scope"] = "mcp:tools offline_access", ["state"] = "state<&=+",
            ["code_challenge"] = new string('a', 43), ["code_challenge_method"] = "S256", ["lang"] = "en"
        });
        using var authorize = await client.GetAsync(url);
        var html = await authorize.Content.ReadAsStringAsync();
        Assert.That(authorize.StatusCode, Is.EqualTo(HttpStatusCode.OK), html);
        Assert.That(html, Does.Contain("<html lang=\"en\">").And.Contain("Sign in and allow").And.Contain("&lt;script&gt;"));
        var fields = Regex.Matches(html, "<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\"")
            .ToDictionary(match => WebUtility.HtmlDecode(match.Groups[1].Value), match => WebUtility.HtmlDecode(match.Groups[2].Value));
        Assert.That(fields["state"], Is.EqualTo("state<&=+"));
        Assert.That(fields["lang"], Is.EqualTo("en"));
        fields["userName"] = "private-user";
        fields["password"] = "private-password";
        fields["decision"] = "allow";
        using var failed = await client.PostAsync("/oauth/login", new FormUrlEncodedContent(fields));
        var failedHtml = await failed.Content.ReadAsStringAsync();
        Assert.That(failed.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(failedHtml, Does.Contain("Invalid username or password.").And.Not.Contain("private-user").And.Not.Contain("private-password"));
        var turkishLink = WebUtility.HtmlDecode(Regex.Match(failedHtml, "<a href=\"([^\"]+)\" lang=\"tr\"").Groups[1].Value);
        Assert.That(turkishLink, Does.StartWith("/oauth/authorize?").And.Not.Contain("__RequestVerificationToken"));
        var query = QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, turkishLink).Query);
        Assert.That(query["state"].ToString(), Is.EqualTo("state<&=+"));
        Assert.That(query["code_challenge"].ToString(), Is.EqualTo(new string('a', 43)));
        Assert.That(await client.GetStringAsync(turkishLink), Does.Contain("<html lang=\"tr\">").And.Contain("Giriş yapın"));
        fields.Remove("__RequestVerificationToken");
        using var expired = await client.PostAsync("/oauth/login", new FormUrlEncodedContent(fields));
        Assert.That(expired.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await expired.Content.ReadAsStringAsync(), Does.Contain("The form has expired."));
    }

    private sealed class MemoryWork : IUnitOfWork
    {
        public DbConnection Connection => throw new NotSupportedException();
        public DbTransaction? Transaction => null;
        public Task<TResult> ExecuteQueryAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default) => action(ct);
        public Task<TResult> ExecuteCommandAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default) => action(ct);
    }

    private sealed class MemoryClients : IOAuthClientRepository
    {
        public Task<OAuthClient?> GetByClientIdAsync(string clientId, CancellationToken ct = default) => Task.FromResult<OAuthClient?>(clientId == "website-test"
            ? new() { ClientId = clientId, DisplayName = "<script>alert(1)</script>", RedirectUris = ["http://127.0.0.1:43434/callback"] } : null);
        public Task<bool> TryInsertAsync(OAuthClient client, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class RejectedLogin : IAsyncCommandHandler<LoginAsync.Command, LoginAsync.Result?>
    {
        public Task<LoginAsync.Result?> HandleAsync(LoginAsync.Command command, CancellationToken ct = default) => Task.FromResult<LoginAsync.Result?>(null);
    }
}
