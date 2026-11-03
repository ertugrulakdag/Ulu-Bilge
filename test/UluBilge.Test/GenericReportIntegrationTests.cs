using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UluBilge.Api.Controllers;
using UluBilge.Api.Extentions;
using UluBilge.Api.MCP.Tools;
using UluBilge.Api.Services;
using UluBilge.App.Authentication;
using UluBilge.App.Cqrs;
using UluBilge.App.Queries.ReportHandler;
using UluBilge.Core.Enums;
using UluBilge.Core.Models;
using UluBilge.Data.Abstract;
using UluBilge.Data.Database;
using UluBilge.Data.Repository;

namespace UluBilge.Test;

// Only this fixture's uniquely named database is created, changed and dropped.
[TestFixture, NonParallelizable]
public sealed class GenericReportIntegrationTests
{
    private readonly string databaseName = "UluBilgeGenericTests_" + Guid.NewGuid().ToString("N");
    private readonly GenericReportOptions options = new() { ExcludedTables = ["DBO.USERS", "dbo.HiddenCustomerView"], MaxRows = 250 };
    private string masterConnectionString = null!;
    private DatabaseConnectionSettings settings = null!;
    private DatabaseSchemaProvider provider = null!;
    private bool databaseCreated;

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "UluBilge.slnx"))) root = root.Parent;
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(root!.FullName, "src/UluBilge.Api/appsettings.json")).Build();
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ULUBILGE_TEST_CONNECTION")
            ?? configuration.GetConnectionString("WriteConnection")) { Pooling = false, InitialCatalog = "master" };
        masterConnectionString = connection.ConnectionString;
        ValidateDatabaseName();
        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync();
            await master.ExecuteAsync($"CREATE DATABASE [{databaseName}]");
            databaseCreated = true;
        }
        connection.InitialCatalog = databaseName;
        settings = new DatabaseConnectionSettings(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReadConnection"] = connection.ConnectionString,
            ["ConnectionStrings:WriteConnection"] = connection.ConnectionString
        }).Build());
        await ExecuteAsync("""
            CREATE TABLE dbo.Users (Id int NOT NULL PRIMARY KEY, Secret nvarchar(50));
            CREATE TABLE dbo.Customers (Id int NOT NULL PRIMARY KEY, Country nvarchar(40) NULL, UserId int NULL REFERENCES dbo.Users(Id));
            CREATE TABLE dbo.Orders (CustomerId int NOT NULL REFERENCES dbo.Customers(Id), Id int NOT NULL,
                Amount decimal(12,2) NOT NULL, PRIMARY KEY (CustomerId, Id));
            CREATE TABLE dbo.Computed (Id int, Value AS Id + 1);
            CREATE TABLE dbo.ClrTable (Id int, Location geography);
            CREATE TABLE dbo.Documents (Id int NOT NULL, XmlValue xml, TextValue nvarchar(max), BinaryValue varbinary(max));
            CREATE TABLE dbo.Numbers (Id int NOT NULL PRIMARY KEY);
            CREATE TABLE dbo.CurrentRecords (Id int NOT NULL PRIMARY KEY,
                ValidFrom datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
                ValidTo datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
                PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo))
                WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.RecordHistory));
            INSERT dbo.Users VALUES (1, N'secret');
            INSERT dbo.Customers VALUES (1, N'Germany', 1), (2, N'France', NULL), (3, NULL, NULL);
            INSERT dbo.Orders VALUES (1, 1, 12.50), (1, 2, 20.00), (2, 3, 5.00);
            INSERT dbo.Documents VALUES (1, N'<value>ok</value>', REPLICATE(CAST(N'Ğ' AS nvarchar(max)), 10000), CAST(REPLICATE(CAST('x' AS varchar(max)), 10000) AS varbinary(max)));
            WITH n AS (SELECT 1 AS Id UNION ALL SELECT Id + 1 FROM n WHERE Id < 100)
            INSERT dbo.Numbers SELECT a.Id + b.Id * 100 FROM n a CROSS JOIN n b;
            """);
        await ExecuteAsync("CREATE VIEW dbo.CustomerView AS SELECT * FROM dbo.Customers");
        await ExecuteAsync("CREATE SYNONYM dbo.CustomerSynonym FOR dbo.Customers");
        foreach (var sql in new[]
        {
            "CREATE VIEW dbo.NestedCustomerView AS SELECT Id, Country FROM dbo.CustomerView",
            "CREATE VIEW dbo.UnqualifiedCustomerView AS SELECT Id, Country FROM Customers",
            "CREATE VIEW dbo.CustomerSummary AS SELECT c.Country, COUNT(*) AS CustomerCount FROM dbo.Customers c GROUP BY c.Country",
            "CREATE VIEW dbo.ConstantView AS SELECT 1 AS Value",
            "CREATE VIEW dbo.BoundCustomerView WITH SCHEMABINDING AS SELECT c.Id, c.Country FROM dbo.Customers c",
            "CREATE VIEW dbo.CteCustomerView AS WITH c AS (SELECT Id, Country FROM dbo.Customers) SELECT Id, Country FROM c",
            "CREATE VIEW dbo.HiddenCustomerView AS SELECT Id, Country FROM dbo.Customers",
            "CREATE VIEW dbo.HiddenNestedView AS SELECT * FROM dbo.HiddenCustomerView",
            "CREATE VIEW dbo.UserView AS SELECT Id, Secret FROM dbo.Users",
            "CREATE VIEW dbo.NestedUserView AS SELECT * FROM dbo.UserView",
            "CREATE VIEW dbo.SynonymView AS SELECT * FROM dbo.CustomerSynonym",
            "CREATE VIEW dbo.ComputedView AS SELECT * FROM dbo.Computed",
            "CREATE VIEW dbo.ClrView AS SELECT Id FROM dbo.ClrTable",
            "CREATE VIEW dbo.HistoryView AS SELECT Id FROM dbo.RecordHistory",
            "CREATE VIEW dbo.CrossDatabaseView AS SELECT name FROM master.sys.databases",
            "CREATE VIEW dbo.SystemView AS SELECT name FROM sys.tables",
            "CREATE VIEW dbo.HintedView AS SELECT Id FROM dbo.Customers WITH (NOLOCK)",
            "CREATE VIEW dbo.SessionView AS SELECT SUSER_SNAME() AS UserName",
            "CREATE VIEW dbo.EncryptedView WITH ENCRYPTION AS SELECT Id FROM dbo.Customers",
            "CREATE FUNCTION dbo.ReportFunction() RETURNS int AS BEGIN RETURN 42; END",
            "CREATE VIEW dbo.FunctionView AS SELECT dbo.ReportFunction() AS Value",
            "CREATE TABLE dbo.DroppedSource (Id int)",
            "CREATE VIEW dbo.BrokenView AS SELECT Id FROM dbo.DroppedSource",
            "DROP TABLE dbo.DroppedSource"
        })
            await ExecuteAsync(sql);
        provider = new DatabaseSchemaProvider(settings, options);
        await new DatabaseSchemaHostedService(provider, NullLogger<DatabaseSchemaHostedService>.Instance).StartAsync(default);
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        provider?.Dispose();
        if (!databaseCreated) return;
        ValidateDatabaseName();
        await using var master = new SqlConnection(masterConnectionString);
        await master.OpenAsync();
        await master.ExecuteAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]");
    }

    [Test]
    public void SnapshotContainsOnlySupportedTablesAndAllowedRelationships()
    {
        var schema = DatabaseSchemaTool.GetDatabaseSchema(provider, options).Data.Schema;
        Assert.That(schema.Tables.Where(table => table.TableOrViewType == TableOrViewEnum.Table).Select(table => table.TableName),
            Is.EquivalentTo(new[] { "Customers", "Orders", "Documents", "Numbers", "CurrentRecords" }));
        Assert.That(schema.Tables.Where(table => table.TableOrViewType == TableOrViewEnum.View).Select(table => table.TableName),
            Is.EquivalentTo(new[] { "CustomerView", "NestedCustomerView", "UnqualifiedCustomerView", "CustomerSummary", "ConstantView", "BoundCustomerView", "CteCustomerView" }));
        var orders = schema.Tables.Single(table => table.TableName == "Orders");
        Assert.Multiple(() =>
        {
            Assert.That(schema.Version, Has.Length.EqualTo(64));
            Assert.That(orders.Columns.Take(2).Select(column => column.PrimaryKeyOrder), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(orders.ForeignKeys.Single().ReferencedTableName, Is.EqualTo("Customers"));
            Assert.That(schema.Tables.Single(table => table.TableName == "Customers").ForeignKeys, Is.Empty);
            Assert.That(schema.Tables.Single(table => table.TableName == "Customers").Columns.Single(column => column.Name == "Country").IsNullable, Is.True);
        });
        Assert.Throws<NotSupportedException>(() => ((IList<DatabaseTableSchema>)schema.Tables).Clear());
        var view = schema.Tables.Single(table => table.TableName == "CustomerView");
        Assert.That(view.Columns.Select(column => column.Name), Is.EqualTo(new[] { "Id", "Country", "UserId" }));
        Assert.That(view.Columns.All(column => column.PrimaryKeyOrder is null), Is.True);
        Assert.That(view.ForeignKeys, Is.Empty);
        var json = JsonSerializer.SerializeToElement(schema, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.That(json.GetProperty("tables").EnumerateArray().Single(table => table.GetProperty("tableName").GetString() == "Customers").GetProperty("tableOrViewType").GetInt32(), Is.EqualTo(1));
        Assert.That(json.GetProperty("tables").EnumerateArray().Single(table => table.GetProperty("tableName").GetString() == "CustomerView").GetProperty("tableOrViewType").GetInt32(), Is.EqualTo(2));
    }

    [TestCase("CustomerView")]
    [TestCase("NestedCustomerView")]
    [TestCase("UnqualifiedCustomerView")]
    [TestCase("BoundCustomerView")]
    [TestCase("CteCustomerView")]
    public async Task AllowedViewsSupportParameterizedReportsAndJoins(string viewName)
    {
        var report = await QueryAsync($"SELECT v.Country, SUM(o.Amount) AS Sales FROM dbo.{viewName} v INNER JOIN dbo.Orders o ON o.CustomerId = v.Id WHERE v.Country = @Country GROUP BY v.Country",
            [Parameter("Country", GenericReportParameterType.String, "Germany")]);
        Assert.That(report.Rows.Single()["Country"], Is.EqualTo("Germany"));
        Assert.That(report.Rows.Single()["Sales"], Is.EqualTo(32.50m));
    }

    [TestCase("HiddenCustomerView")]
    [TestCase("HiddenNestedView")]
    [TestCase("UserView")]
    [TestCase("NestedUserView")]
    [TestCase("SynonymView")]
    [TestCase("ComputedView")]
    [TestCase("ClrView")]
    [TestCase("HistoryView")]
    [TestCase("CrossDatabaseView")]
    [TestCase("SystemView")]
    [TestCase("HintedView")]
    [TestCase("SessionView")]
    [TestCase("EncryptedView")]
    [TestCase("FunctionView")]
    [TestCase("BrokenView")]
    public void RejectsViewsThatBypassTheReportPolicy(string viewName) =>
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync($"SELECT * FROM dbo.{viewName}"));

    [Test]
    public async Task ViewDefinitionChangesVersionEvenWhenColumnsStayTheSame()
    {
        await ExecuteAsync("CREATE VIEW dbo.VersionedView AS SELECT c.Id FROM dbo.Customers c WHERE c.Id > 1");
        try
        {
            using var before = new DatabaseSchemaProvider(settings, options);
            await before.InitializeAsync();
            await ExecuteAsync("ALTER VIEW dbo.VersionedView AS SELECT c.Id FROM dbo.Customers c WHERE c.Id > 2");
            using var after = new DatabaseSchemaProvider(settings, options);
            await after.InitializeAsync();
            Assert.That(after.Snapshot.Tables.Select(table => table.TableName), Is.EqualTo(before.Snapshot.Tables.Select(table => table.TableName)));
            Assert.That(after.Snapshot.Version, Is.Not.EqualTo(before.Snapshot.Version));
            await before.InitializeAsync();
            Assert.That(before.Snapshot.Tables.Single(table => table.TableName == "VersionedView").TableOrViewType, Is.EqualTo(TableOrViewEnum.View));
        }
        finally { await ExecuteAsync("DROP VIEW dbo.VersionedView"); }
    }

    [Test]
    public async Task SnapshotStaysFixedUntilRestartAndVersionTracksMetadata()
    {
        using var first = new DatabaseSchemaProvider(settings, options);
        await first.InitializeAsync();
        var before = first.Snapshot;
        await ExecuteAsync("CREATE TABLE dbo.NewMetadata (Id int NOT NULL)");
        try
        {
            await first.InitializeAsync();
            Assert.That(first.Snapshot, Is.SameAs(before));
            using var restarted = new DatabaseSchemaProvider(settings, options);
            await restarted.InitializeAsync();
            Assert.That(restarted.Snapshot.Version, Is.Not.EqualTo(before.Version));
            Assert.That(restarted.Snapshot.Tables.Select(table => table.TableName), Does.Contain("NewMetadata"));
        }
        finally { await ExecuteAsync("DROP TABLE dbo.NewMetadata"); }
        using var identical = new DatabaseSchemaProvider(settings, options);
        await identical.InitializeAsync();
        Assert.That(identical.Snapshot.Version, Is.EqualTo(before.Version));
    }

    [Test]
    public async Task EmptyExclusionsAreSupported()
    {
        using var all = new DatabaseSchemaProvider(settings, new GenericReportOptions());
        await all.InitializeAsync();
        Assert.That(all.Snapshot.Tables.Select(table => table.TableName), Does.Contain("Users"));
        Assert.That(all.Snapshot.Tables.Select(table => table.TableName), Does.Contain("UserView").And.Contain("NestedUserView").And.Contain("HiddenCustomerView"));
    }

    [Test]
    public void InitialLoadFailureStopsStartup()
    {
        var invalid = new SqlConnectionStringBuilder(settings.ReadConnection) { InitialCatalog = databaseName + "_Missing" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReadConnection"] = invalid.ConnectionString,
            ["ConnectionStrings:WriteConnection"] = invalid.ConnectionString
        }).Build();
        using var unavailable = new DatabaseSchemaProvider(new DatabaseConnectionSettings(configuration), options);
        var startup = new DatabaseSchemaHostedService(unavailable, NullLogger<DatabaseSchemaHostedService>.Instance);
        Assert.ThrowsAsync<SqlException>(() => startup.StartAsync(default));
        Assert.Throws<InvalidOperationException>(() => _ = unavailable.Snapshot);
    }

    [Test]
    public async Task ParameterizedQueryRunsThroughExistingQueryDecorator()
    {
        var report = await QueryAsync("SELECT c.Country AS Country, COUNT(*) AS CustomerCount FROM dbo.Customers c WHERE c.Country = @Country GROUP BY c.Country",
            [Parameter("Country", GenericReportParameterType.String, "Germany")]);
        Assert.Multiple(() =>
        {
            Assert.That(report.SVersion, Is.EqualTo(provider.Snapshot.Version));
            Assert.That(report.ReturnedRowCount, Is.EqualTo(1));
            Assert.That(report.IsTruncated, Is.False);
            Assert.That(report.Rows[0]["Country"], Is.EqualTo("Germany"));
            Assert.That(report.Rows[0]["CustomerCount"], Is.EqualTo(1));
            Assert.That(report.Columns.Select(column => column.DataType), Is.EqualTo(new[] { "nvarchar", "int" }));
        });
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT u.Secret FROM dbo.Users u"));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT 1 AS Value", version: new string('0', 64)));
        var injectedValue = await QueryAsync("SELECT c.Id FROM dbo.Customers c WHERE c.Country = @Country",
            [Parameter("Country", GenericReportParameterType.String, "Germany'; DROP TABLE dbo.Customers;--")]);
        Assert.That(injectedValue.Rows, Is.Empty);
    }

    [TestCase(GenericReportParameterType.String, "Germany")]
    [TestCase(GenericReportParameterType.Int32, "42")]
    [TestCase(GenericReportParameterType.Int64, "4294967296")]
    [TestCase(GenericReportParameterType.Decimal, "1234.5678")]
    [TestCase(GenericReportParameterType.Double, "1.25")]
    [TestCase(GenericReportParameterType.Boolean, "true")]
    [TestCase(GenericReportParameterType.Date, "2026-11-01")]
    [TestCase(GenericReportParameterType.DateTime, "2026-11-01T12:30:00")]
    [TestCase(GenericReportParameterType.DateTimeOffset, "2026-11-01T12:30:00+03:00")]
    [TestCase(GenericReportParameterType.Guid, "3ce301d2-036f-41cd-9146-42db74788399")]
    public async Task ConvertsParametersAndTypedNulls(GenericReportParameterType type, string text)
    {
        var value = type is GenericReportParameterType.Int32 or GenericReportParameterType.Int64 or GenericReportParameterType.Decimal
            or GenericReportParameterType.Double or GenericReportParameterType.Boolean ? JsonDocument.Parse(text).RootElement.Clone() : JsonSerializer.SerializeToElement(text);
        var report = await QueryAsync("SELECT @Value AS Value", [new() { Name = "Value", Type = type, Value = value }]);
        Assert.That(report.Rows[0]["Value"], Is.Not.Null);
        var empty = await QueryAsync("SELECT @Value AS Value", [Parameter("Value", type, null)]);
        Assert.That(empty.Rows[0]["Value"], Is.Null);
    }

    [Test]
    public void RejectsInvalidParametersAndResultColumns()
    {
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT @Value AS Value", [Parameter("Value", GenericReportParameterType.String, new string('x', 4001))]));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT @Value AS Value", [Parameter("Value", GenericReportParameterType.Int32, "bad")]));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT @Value AS Value", [Parameter("Value", GenericReportParameterType.Date, "01/11/2026")]));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT @Value AS Value", [Parameter("Value", GenericReportParameterType.String, "a"), Parameter("@value", GenericReportParameterType.String, "b")]));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT 1 AS Value", Enumerable.Range(0, 101).Select(i => Parameter("P" + i, GenericReportParameterType.Int32, i)).ToArray()));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT 1 AS Value, 2 AS value"));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT 1 + 1"));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT " + string.Join(",", Enumerable.Range(0, 129).Select(i => $"1 AS C{i}"))));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT d.XmlValue FROM dbo.Documents d"));
        Assert.ThrowsAsync<ValidationException>(() => QueryAsync("SELECT 1 AS Value", maxRows: options.MaxRows + 1));
    }

    [Test]
    public async Task RowAndByteLimitsReturnWholeRowsAndTruncationFlag()
    {
        var exact = await QueryAsync("SELECT c.Id FROM dbo.Customers c ORDER BY c.Id", maxRows: 3);
        var limited = await QueryAsync("SELECT c.Id FROM dbo.Customers c ORDER BY c.Id", maxRows: 2);
        var defaults = await QueryAsync("SELECT n.Id FROM dbo.Numbers n ORDER BY n.Id");
        Assert.That(exact.IsTruncated, Is.False);
        Assert.That(limited.ReturnedRowCount, Is.EqualTo(2));
        Assert.That(limited.IsTruncated, Is.True);
        Assert.That(defaults.ReturnedRowCount, Is.EqualTo(200));
        Assert.That(defaults.IsTruncated, Is.True);
        var smallDefault = await QueryAsync("SELECT c.Id FROM dbo.Customers c", limits: new GenericReportOptions { MaxRows = 2 });
        Assert.That(smallDefault.ReturnedRowCount, Is.EqualTo(2));
        Assert.That(smallDefault.IsTruncated, Is.True);
        var tiny = new GenericReportOptions { MaxResultBytes = 1024 };
        foreach (var column in new[] { "TextValue", "BinaryValue" })
        {
            var large = await QueryAsync($"SELECT d.{column} FROM dbo.Documents d", limits: tiny);
            Assert.That(large.IsTruncated, Is.True);
            Assert.That(large.Rows, Is.Empty);
            Assert.That(JsonSerializer.SerializeToUtf8Bytes(large).Length, Is.LessThanOrEqualTo(tiny.MaxResultBytes));
        }
        var xml = await QueryAsync("SELECT CAST(d.XmlValue AS nvarchar(max)) AS XmlText FROM dbo.Documents d");
        Assert.That(xml.Rows[0]["XmlText"], Is.EqualTo("<value>ok</value>"));
    }

    [Test]
    public async Task SqlExecutionHonorsTotalTimeoutAndRequestCancellation()
    {
        const string expensive = "SELECT SUM(CONVERT(bigint, a.Id) + b.Id + c.Id) AS Total FROM dbo.Numbers a CROSS JOIN dbo.Numbers b CROSS JOIN dbo.Numbers c";
        var clock = Stopwatch.StartNew();
        var timeout = Assert.ThrowsAsync<ValidationException>(() => QueryAsync(expensive, limits: new GenericReportOptions { TimeoutSeconds = 1 }));
        Assert.That(timeout!.Message, Does.Contain("süre sınırını"));
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(8)));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        try
        {
            await QueryAsync(expensive, ct: cancellation.Token);
            Assert.Fail("Query should have been cancelled.");
        }
        catch (OperationCanceledException) { Assert.That(cancellation.IsCancellationRequested, Is.True); }
    }

    [Test]
    public async Task HttpAndMcpUseTheSameQueryAndExistingAuthorization()
    {
        const string serverUrl = "https://localhost:7187";
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development", ApplicationName = typeof(ReportController).Assembly.FullName
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReadConnection"] = settings.ReadConnection,
            ["ConnectionStrings:WriteConnection"] = settings.WriteConnection,
            ["Jwt:Issuer"] = "GenericTests", ["Jwt:Audience"] = "GenericTests.Rest",
            ["Jwt:SigningKeyBase64"] = Convert.ToBase64String(key.Key), ["OAuth:ServerUrl"] = serverUrl
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(ReportController).Assembly);
        builder.Services.AddUluBilgeServices(builder.Configuration);
        builder.Services.AddSingleton<IDatabaseSchemaProvider>(provider);
        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<DatabaseSchemaHostedService>();
        builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);
        builder.Services.AddMcpOAuth(builder.Configuration, builder.Environment);
        builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly(typeof(GenericReportTool).Assembly);
        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapMcpOAuth();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri(serverUrl);
        var arguments = new
        {
            sql = "SELECT c.Country AS Country FROM dbo.CustomerView c WHERE c.Country = @Country",
            sVersion = provider.Snapshot.Version,
            parameters = new[] { new { name = "Country", type = "String", value = "Germany" } }, maxRows = 100
        };
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(false, "Report"));
        using var httpResponse = await client.PostAsJsonAsync("/v1/Report/GetGenericReport", arguments);
        Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var httpData = (await httpResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.That(httpData.GetProperty("rows")[0].GetProperty("Country").GetString(), Is.EqualTo("Germany"));
        Assert.That(httpData.GetProperty("sVersion").GetString(), Is.EqualTo(provider.Snapshot.Version));
        using var get = await client.GetAsync("/v1/Report/GetGenericReport");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
#if !DEBUG
        client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await client.PostAsJsonAsync("/v1/Report/GetGenericReport", arguments);
        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(false, "Region"));
        using var forbidden = await client.PostAsJsonAsync("/v1/Report/GetGenericReport", arguments);
        Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
#endif
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(true, "Report"));
        var schemaCall = await McpAsync("tools/call", new { name = "get_database_schema", arguments = new { } });
        var schemaData = schemaCall.GetProperty("result").GetProperty("structuredContent").GetProperty("data");
        Assert.That(schemaData.GetProperty("schema").GetProperty("version").GetString(), Is.EqualTo(provider.Snapshot.Version));
        Assert.That(schemaData.GetProperty("maxRows").GetInt32(), Is.EqualTo(options.MaxRows));
        Assert.That(schemaData.GetProperty("schema").GetProperty("tables").EnumerateArray()
            .Single(table => table.GetProperty("tableName").GetString() == "CustomerView").GetProperty("tableOrViewType").GetInt32(), Is.EqualTo(2));
        var reportCall = await McpAsync("tools/call", new { name = "get_generic_report", arguments });
        var reportData = reportCall.GetProperty("result").GetProperty("structuredContent").GetProperty("data");
        Assert.That(reportData.GetRawText(), Is.EqualTo(httpData.GetRawText()));
        var stale = await McpAsync("tools/call", new { name = "get_generic_report", arguments = new { sql = "SELECT 1 AS Value", sVersion = new string('0', 64) } });
        Assert.That(stale.GetProperty("result").GetProperty("isError").GetBoolean(), Is.True);
        foreach (var token in new[] { Token(true, "None"), Token(true, "Report", "offline_access"), Token(false, "Report") })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            using var denied = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" });
            Assert.That(denied.StatusCode, Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden));
        }

        string Token(bool mcp, string role, string scope = "mcp:tools") => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = mcp ? serverUrl + "/" : "GenericTests", Audience = mcp ? serverUrl + "/mcp" : "GenericTests.Rest",
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            IssuedAt = DateTime.UtcNow, NotBefore = DateTime.UtcNow, Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { [JwtOptions.RoleClaimType] = role, ["scope"] = scope, ["sub"] = "1" }
        });

        async Task<JsonElement> McpAsync(string method, object parameters)
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
    }

    private async Task<GetGenericReportAsync.Result> QueryAsync(string sql, IReadOnlyList<GenericReportParameter>? parameters = null,
        int? maxRows = null, string? version = null, GenericReportOptions? limits = null, CancellationToken ct = default)
    {
        var configuration = limits ?? options;
        var work = new UnitOfWork(settings, NullLogger<UnitOfWork>.Instance);
        var repository = new GenericReportRepository(work, provider, new GenericReportSqlValidator(), configuration);
        var handler = new QueryHandlerDecorator<GetGenericReportAsync.Query, GetGenericReportAsync.Result>(new GetGenericReportAsync.Handler(repository, configuration), work);
        return await handler.HandleAsync(new() { Sql = sql, SVersion = version ?? provider.Snapshot.Version, Parameters = parameters ?? [], MaxRows = maxRows }, ct);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(settings.ReadConnection);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql);
    }

    private static GenericReportParameter Parameter(string name, GenericReportParameterType type, object? value) =>
        new() { Name = name, Type = type, Value = JsonSerializer.SerializeToElement(value) };

    private void ValidateDatabaseName()
    {
        if (!Regex.IsMatch(databaseName, "^UluBilgeGenericTests_[a-f0-9]{32}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Unexpected test database name.");
    }
}
