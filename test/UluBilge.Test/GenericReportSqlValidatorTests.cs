using System.ComponentModel.DataAnnotations;
using UluBilge.Core.Enums;
using UluBilge.Core.Models;
using UluBilge.Data.Database;

namespace UluBilge.Test;

[TestFixture]
public sealed class GenericReportSqlValidatorTests
{
    private readonly GenericReportSqlValidator validator = new();
    private readonly DatabaseSchemaSnapshot schema = new()
    {
        Version = new string('A', 64), LoadedAtUtc = DateTimeOffset.UtcNow, Collation = "Latin1_General_CI_AS",
        Tables =
        [
            new() { SchemaName = "dbo", TableName = "Customers", TableOrViewType = TableOrViewEnum.Table, Columns = [], ForeignKeys = [] },
            new() { SchemaName = "dbo", TableName = "CustomerView", TableOrViewType = TableOrViewEnum.View, Columns = [], ForeignKeys = [] }
        ]
    };

    [TestCase("SELECT c.Country AS Country, COUNT(*) AS Total FROM dbo.Customers c GROUP BY c.Country;")]
    [TestCase("WITH countries AS (SELECT c.Country FROM dbo.Customers c) SELECT c.Country FROM countries c;")]
    [TestCase("SELECT c.Country FROM [dbo].[Customers] c WHERE EXISTS (SELECT 1 FROM dbo.Customers x WHERE x.Country = c.Country);")]
    [TestCase("SELECT ROW_NUMBER() OVER (ORDER BY c.Country) AS RowId, c.Country FROM dbo.Customers c;")]
    [TestCase("SELECT CAST(NULL AS nvarchar(max)) AS Value;")]
    [TestCase("SELECT v.Country FROM dbo.CustomerView v;")]
    [TestCase("SELECT YEAR(c.CreatedAt) AS Year, SUM(CASE WHEN c.Country IS NULL THEN 0 ELSE 1 END) AS Total FROM dbo.Customers c GROUP BY YEAR(c.CreatedAt);")]
    public void AllowsReportingSelects(string sql) => Assert.DoesNotThrow(() => validator.Validate(sql, [], schema));

    [TestCase("SELECT * FROM dbo.Users")]
    [TestCase("SELECT * FROM dbo.SecretView")]
    [TestCase("SELECT * FROM dbo.customerView")]
    [TestCase("SELECT * FROM dbo.customers")]
    [TestCase("SELECT * FROM Customers")]
    [TestCase("SELECT * FROM Northwind.dbo.Customers")]
    [TestCase("SELECT * FROM server.Northwind.dbo.Customers")]
    [TestCase("SELECT * FROM sys.tables")]
    [TestCase("SELECT * FROM INFORMATION_SCHEMA.TABLES")]
    [TestCase("SELECT * FROM dbo.Customers WITH (NOLOCK)")]
    [TestCase("SELECT * FROM dbo.Customers OPTION (MAXDOP 1)")]
    [TestCase("SELECT * FROM dbo.Customers TABLESAMPLE (10 ROWS)")]
    [TestCase("SELECT * FROM dbo.Customers FOR SYSTEM_TIME ALL")]
    [TestCase("SELECT * INTO dbo.Other FROM dbo.Customers")]
    [TestCase("SELECT 1 AS Value; SELECT 2 AS Value;")]
    [TestCase("SELECT 1 AS Value\nGO")]
    [TestCase("DELETE FROM dbo.Customers")]
    [TestCase("UPDATE dbo.Customers SET Country = 'Germany'")]
    [TestCase("INSERT dbo.Customers (Country) VALUES ('Germany')")]
    [TestCase("DROP TABLE dbo.Customers")]
    [TestCase("EXEC sp_help")]
    [TestCase("SELECT dbo.MyFunction() AS Value")]
    [TestCase("SELECT * FROM dbo.MyFunction()")]
    [TestCase("SELECT @@VERSION AS Value")]
    [TestCase("SELECT SUSER_SNAME() AS Value")]
    [TestCase("SELECT NEXT VALUE FOR dbo.MySequence AS Value")]
    [TestCase("SELECT * FROM OPENQUERY(server, 'SELECT * FROM dbo.Users')")]
    [TestCase("SELECT c.Country FROM dbo.Customers c FOR XML AUTO")]
    [TestCase("SELECT c.Country FROM dbo.Customers c INNER HASH JOIN dbo.Customers x ON c.Country = x.Country")]
    public void RejectsUnsupportedOrUnauthorizedSql(string sql) =>
        Assert.Throws<ValidationException>(() => validator.Validate(sql, [], schema));

    [Test]
    public void RequiresExactlyTheSuppliedParameters()
    {
        const string sql = "SELECT c.Country FROM dbo.Customers c WHERE c.Country = @Country";
        Assert.DoesNotThrow(() => validator.Validate(sql, ["@Country"], schema));
        Assert.Throws<ValidationException>(() => validator.Validate(sql, [], schema));
        Assert.Throws<ValidationException>(() => validator.Validate(sql, ["@Country", "@Unused"], schema));
        Assert.Throws<ValidationException>(() => validator.Validate(sql, ["@Country", "@country"], schema));
    }

    [Test]
    public void LimitsLengthAndComplexityBeforeExecutingSql()
    {
        Assert.Throws<ValidationException>(() => validator.Validate("SELECT 1 AS Value" + new string(' ', 32768), [], schema));
        Assert.Throws<ValidationException>(() => validator.Validate("SELECT " + new string('(', 129) + "1" + new string(')', 129) + " AS Value", [], schema));
    }
}
