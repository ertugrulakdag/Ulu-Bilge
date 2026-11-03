using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using UluBilge.Api.Extentions;

namespace UluBilge.Test;

[TestFixture]
public sealed class McpOAuthConfigurationTests
{
    [TestCase("Development", "http://example.com:5156")]
    [TestCase("Development", "http://192.168.1.10:5156")]
    [TestCase("Production", "http://localhost:5156")]
    [TestCase("Staging", "http://127.0.0.1:5156")]
    [TestCase("Production", "https://localhost:7187")]
    [TestCase("Development", "http://localhost:5156/mcp")]
    [TestCase("Development", "http://user@localhost:5156")]
    [TestCase("Development", "invalid")]
    public void InvalidOrNonLocalHttpIssuer_IsRejectedAtStartup(string environment, string serverUrl)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["OAuth:ServerUrl"] = serverUrl });
        Assert.Throws<InvalidOperationException>(() => builder.Services.AddMcpOAuth(builder.Configuration, builder.Environment));
    }
}
