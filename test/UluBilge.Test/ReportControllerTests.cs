using System.Net;
using System.Net.Http.Json;
using UluBilge.Api.Models;
using UluBilge.App.Queries.ReportHandler;

namespace UluBilge.Test;

[TestFixture]
public sealed class ReportControllerTests
{
    private ApiTestClient api = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        api = new ApiTestClient();
        await api.LoginAsync();
    }

    [OneTimeTearDown]
    public void TearDown() => api?.Dispose();

    [Test]
    public async Task CustomerSales_DefaultLimit_YearFilter_AndEmptyYear()
    {
        var defaults = await GetAsync<GetCustomerSalesAsync.Result>("GetCustomerSales");
        var all = await GetAsync<GetCustomerSalesAsync.Result>("GetCustomerSales?topCount=50");
        var year = await GetAsync<GetCustomerSalesAsync.Result>("GetCustomerSales?year=1997&topCount=100");
        var top = await GetAsync<GetCustomerSalesAsync.Result>("GetCustomerSales?year=1997&topCount=3");
        var empty = await GetAsync<GetCustomerSalesAsync.Result>("GetCustomerSales?year=1753");

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Data.Select(item => item.CustomerId), Is.EqualTo(all.Data.Take(10).Select(item => item.CustomerId)));
            Assert.That(year.Data, Is.Not.Empty);
            Assert.That(year.Data.Select(item => item.CustomerId), Is.Unique);
            Assert.That(year.Data.Select(item => item.CustomerId), Is.EqualTo(year.Data.OrderByDescending(item => item.NetSales).ThenBy(item => item.CustomerId, StringComparer.Ordinal).Select(item => item.CustomerId)));
            Assert.That(top.Data.Select(item => item.CustomerId), Is.EqualTo(year.Data.Take(3).Select(item => item.CustomerId)));
            Assert.That(all.Data.First().NetSales, Is.GreaterThanOrEqualTo(year.Data.Max(item => item.NetSales)));
            Assert.That(empty.Data, Is.Empty);
        });
    }

    [Test]
    public async Task EmployeeSales_DefaultLimit_YearFilter_AndEmptyYear()
    {
        var defaults = await GetAsync<GetEmployeeSalesAsync.Result>("GetEmployeeSales");
        var all = await GetAsync<GetEmployeeSalesAsync.Result>("GetEmployeeSales?topCount=50");
        var year = await GetAsync<GetEmployeeSalesAsync.Result>("GetEmployeeSales?year=1997&topCount=100");
        var top = await GetAsync<GetEmployeeSalesAsync.Result>("GetEmployeeSales?year=1997&topCount=3");
        var empty = await GetAsync<GetEmployeeSalesAsync.Result>("GetEmployeeSales?year=1753");

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Data.Select(item => item.EmployeeId), Is.EqualTo(all.Data.Take(10).Select(item => item.EmployeeId)));
            Assert.That(year.Data, Is.Not.Empty);
            Assert.That(year.Data.Select(item => item.EmployeeId), Is.Unique);
            Assert.That(year.Data.Select(item => item.EmployeeId), Is.EqualTo(year.Data.OrderByDescending(item => item.NetSales).ThenBy(item => item.EmployeeId).Select(item => item.EmployeeId)));
            Assert.That(top.Data.Select(item => item.EmployeeId), Is.EqualTo(year.Data.Take(3).Select(item => item.EmployeeId)));
            Assert.That(all.Data.First().NetSales, Is.GreaterThanOrEqualTo(year.Data.Max(item => item.NetSales)));
            Assert.That(empty.Data, Is.Empty);
        });
    }

    [TestCase("GetCustomerSales")]
    [TestCase("GetEmployeeSales")]
    public async Task InvalidFilters_ReturnBadRequest(string action)
    {
        string[] filters = ["year=1752", $"year={DateTime.UtcNow.Year + 1}", "topCount=0", "topCount=101", "topCount=51", "year=1997&topCount=101"];
        foreach (var filter in filters)
        {
            using var response = await api.Client.GetAsync($"/v1/Report/{action}?{filter}");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), filter);
        }
    }

    [Test]
    public async Task SalesTotals_MatchProductReport_AndOrdersAreCountedOnce()
    {
        // Northwind'de 1997 yılının tüm ürün, müşteri ve çalışanları 100 kayıt sınırına sığar.
        var products = await GetAsync<GetProductSalesAsync.Result>("GetProductSales?year=1997&topCount=100");
        var customers = await GetAsync<GetCustomerSalesAsync.Result>("GetCustomerSales?year=1997&topCount=100");
        var employees = await GetAsync<GetEmployeeSalesAsync.Result>("GetEmployeeSales?year=1997&topCount=100");

        Assert.Multiple(() =>
        {
            Assert.That(customers.Data.Sum(item => item.TotalQuantity), Is.EqualTo(products.Data.Sum(item => item.TotalQuantity)));
            Assert.That(employees.Data.Sum(item => item.TotalQuantity), Is.EqualTo(products.Data.Sum(item => item.TotalQuantity)));
            // Her grup iki ondalığa yuvarlandığı için toplamlar küçük bir yuvarlama farkı içerebilir.
            Assert.That(customers.Data.Sum(item => item.NetSales), Is.EqualTo(products.Data.Sum(item => item.NetSales)).Within(1m));
            Assert.That(employees.Data.Sum(item => item.NetSales), Is.EqualTo(products.Data.Sum(item => item.NetSales)).Within(1m));
            Assert.That(customers.Data.Sum(item => item.OrderCount), Is.EqualTo(employees.Data.Sum(item => item.OrderCount)));
            Assert.That(customers.Data.Sum(item => item.OrderCount), Is.LessThan(products.Data.Sum(item => item.OrderCount)));
        });
    }

    private async Task<TResult> GetAsync<TResult>(string action)
    {
        using var response = await api.Client.GetAsync($"/v1/Report/{action}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<TResult>>();
        Assert.That(body, Is.Not.Null);
        return body!.Data;
    }
}
