using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UluBilge.Api.Controllers;
using UluBilge.Api.Exceptions;
using UluBilge.Api.Extentions;
using UluBilge.Api.MCP.Tools;
using UluBilge.App.Authentication;
using UluBilge.Data.Abstract;

namespace UluBilge.Test;

[TestFixture]
public sealed class FoodMcpTests
{
    private const string ServerUrl = "https://localhost:7187";
    private readonly SymmetricSecurityKey key = new(RandomNumberGenerator.GetBytes(32));
    private WebApplication app = null!;
    private HttpClient client = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development", ApplicationName = typeof(FoodController).Assembly.FullName
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReadConnection"] = "Server=unused;Database=unused",
            ["ConnectionStrings:WriteConnection"] = "Server=unused;Database=unused",
            ["Jwt:Issuer"] = "FoodTests", ["Jwt:Audience"] = "FoodTests.Rest",
            ["Jwt:SigningKeyBase64"] = Convert.ToBase64String(key.Key), ["OAuth:ServerUrl"] = ServerUrl
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(FoodController).Assembly);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddUluBilgeServices(builder.Configuration);
        // Exercise the actual handler scanning/decorator without opening a database for sample menus.
        builder.Services.AddScoped<IUnitOfWork, InMemoryWork>();
        builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);
        builder.Services.AddMcpOAuth(builder.Configuration, builder.Environment);
        builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly(typeof(FoodTool).Assembly);
        app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapMcpOAuth();
        await app.StartAsync();
        client = app.GetTestClient();
        client.BaseAddress = new Uri(ServerUrl);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        client?.Dispose();
        if (app is not null) await app.DisposeAsync();
    }

    [TestCase("Admin", "tr")]
    [TestCase("Food", "en")]
    [TestCase("Report", "tr")]
    [TestCase("Region", "en")]
    public async Task HttpAndMcp_UseSameQueryAndStructuredMenu(string role, string language)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role, mcp: false));
        using var response = await client.GetAsync($"/v1/Food/GetFoods?period=week&date=2026-11-02&offset=1&language={language}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var httpData = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role));
        var call = await McpAsync("tools/call", new
        {
            name = "get_foods", arguments = new { period = "week", date = "2026-11-02", offset = 1, language }
        });
        var mcpData = call.GetProperty("result").GetProperty("structuredContent").GetProperty("data");
        Assert.Multiple(() =>
        {
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(mcpData.GetRawText()), JsonNode.Parse(httpData.GetRawText())), Is.True);
            Assert.That(mcpData.GetProperty("data").GetArrayLength(), Is.EqualTo(7));
            Assert.That(mcpData.GetProperty("data").EnumerateArray().Select(food => food.GetProperty("totalCalorie").GetInt32()), Is.All.GreaterThan(0));
            Assert.That(mcpData.GetProperty("startDate").GetString(), Is.EqualTo("2026-11-09"));
            Assert.That(mcpData.GetProperty("endDate").GetString(), Is.EqualTo("2026-11-15"));
            Assert.That(mcpData.GetProperty("language").GetString(), Is.EqualTo(language));
            Assert.That(mcpData.GetProperty("isSampleData").GetBoolean(), Is.True);
        });
    }

    [TestCase("Food")]
    [TestCase("Region")]
    public async Task FoodAndRegionRoles_DiscoverOnlyFood_AndCannotCallReportTools(string role)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role));
        var list = await McpAsync("tools/list", new { });
        var tools = list.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
        Assert.That(tools.Select(tool => tool.GetProperty("name").GetString()), Is.EqualTo(new[] { "get_foods" }));
        var food = tools.Single();
        Assert.Multiple(() =>
        {
            Assert.That(food.GetProperty("description").GetString(), Does.Contain("öğle yemeği").And.Contain("lunch"));
            Assert.That(food.GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(property => property.Name),
                Is.EquivalentTo(new[] { "period", "date", "offset", "language" }));
            Assert.That(food.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean(), Is.True);
            Assert.That(food.TryGetProperty("outputSchema", out _), Is.True);
        });
        foreach (var name in new[] { "get_database_schema", "get_generic_report" })
        {
            var denied = await McpAsync("tools/call", new { name, arguments = new { sql = "SELECT 1 AS Value", sVersion = "unused" } });
            Assert.That(denied.TryGetProperty("error", out _) || denied.GetProperty("result").GetProperty("isError").GetBoolean(), Is.True);
        }
    }

    [Test]
    public async Task AdminAndReportRoles_StillDiscoverReportTools()
    {
        foreach (var role in new[] { "Admin", "Report" })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role));
            var list = await McpAsync("tools/list", new { });
            Assert.That(list.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()),
                Is.EquivalentTo(new[] { "get_foods", "get_database_schema", "get_generic_report" }));
        }
    }

    [TestCase("get_database_schema")]
    [TestCase("get_foods")]
    [TestCase("get_generic_report")]
    public async Task ToolsList_PublishesExplicitSafetyAnnotations(string toolName)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("Report"));
        var list = await McpAsync("tools/list", new { });
        var tool = list.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == toolName);
        var annotations = tool.GetProperty("annotations");

        Assert.Multiple(() =>
        {
            Assert.That(annotations.TryGetProperty("readOnlyHint", out var readOnly) && readOnly.ValueKind == JsonValueKind.True,
                Is.True, $"{toolName}: readOnlyHint must be an explicit JSON true.");
            Assert.That(annotations.TryGetProperty("destructiveHint", out var destructive) && destructive.ValueKind == JsonValueKind.False,
                Is.True, $"{toolName}: destructiveHint must be an explicit JSON false.");
            Assert.That(annotations.TryGetProperty("openWorldHint", out var openWorld) && openWorld.ValueKind == JsonValueKind.False,
                Is.True, $"{toolName}: openWorldHint must be an explicit JSON false.");
        });
    }

    [Test]
    public async Task DefaultsAndInvalidParameters_ReturnMenusOrUsefulToolErrors()
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("Food"));
        var defaults = await McpAsync("tools/call", new { name = "get_foods", arguments = new { } });
        Assert.That(defaults.GetProperty("result").GetProperty("structuredContent").GetProperty("data").GetProperty("data").GetArrayLength(), Is.EqualTo(1));
        foreach (var arguments in new object[] { new { period = "year" }, new { language = "de" }, new { offset = 367 }, new { date = "invalid" } })
        {
            var invalid = await McpAsync("tools/call", new { name = "get_foods", arguments });
            Assert.That(invalid.GetProperty("result").GetProperty("isError").GetBoolean(), Is.True);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("Food", mcp: false));
        foreach (var query in new[] { "period=year", "language=de", "offset=367", "date=invalid" })
        {
            using var invalid = await client.GetAsync("/v1/Food/GetFoods?" + query);
            Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }
    }

    [Test]
    public async Task Mcp_RejectsAnonymousRestTokensMissingScopeAndUnsupportedRoles()
    {
        foreach (var token in new[] { null, Token("Food", mcp: false), Token("Food", scope: "offline_access"), Token("None") })
        {
            client.DefaultRequestHeaders.Authorization = token is null ? null : new("Bearer", token);
            using var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" });
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden));
        }
#if !DEBUG
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("None", mcp: false));
        using var forbidden = await client.GetAsync("/v1/Food/GetFoods");
        Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
#endif
    }

    private string Token(string role, bool mcp = true, string scope = "mcp:tools") => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = mcp ? ServerUrl + "/" : "FoodTests", Audience = mcp ? ServerUrl + "/mcp" : "FoodTests.Rest",
        SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        IssuedAt = DateTime.UtcNow, NotBefore = DateTime.UtcNow, Expires = DateTime.UtcNow.AddMinutes(5),
        Claims = new Dictionary<string, object> { [JwtOptions.RoleClaimType] = role, ["scope"] = scope, ["sub"] = "1" }
    });

    private async Task<JsonElement> McpAsync(string method, object parameters)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        var json = body.StartsWith("event:", StringComparison.Ordinal) || body.StartsWith("data:", StringComparison.Ordinal)
            ? string.Join("\n", body.Split('\n').Where(line => line.StartsWith("data:", StringComparison.Ordinal)).Select(line => line[5..].Trim())) : body;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class InMemoryWork : IUnitOfWork
    {
        public DbConnection Connection => throw new NotSupportedException();
        public DbTransaction? Transaction => null;
        public Task<TResult> ExecuteQueryAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default) => action(ct);
        public Task<TResult> ExecuteCommandAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
