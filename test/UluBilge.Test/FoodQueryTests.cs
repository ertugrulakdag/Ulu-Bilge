using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using UluBilge.App.Queries.FoodHandler;

namespace UluBilge.Test;

[TestFixture]
public sealed class FoodQueryTests
{
    private readonly GetFoodsAsync.Handler handler = new(new FixedClock());

    [Test]
    public async Task TodayAndTomorrow_UseCompanyDateWhenUtcIsStillYesterday()
    {
        var today = await handler.HandleAsync(new());
        var tomorrow = await handler.HandleAsync(new() { Offset = 1, Language = "en" });
        Assert.Multiple(() =>
        {
            Assert.That(today.Data.Single().Date, Is.EqualTo(new DateOnly(2026, 11, 2)));
            Assert.That(today.TimeZone, Is.EqualTo("Turkey Standard Time"));
            Assert.That(today.IsSampleData, Is.True);
            Assert.That(today.Note, Does.Contain("örnek"));
            Assert.That(tomorrow.Data.Single().Date, Is.EqualTo(new DateOnly(2026, 11, 3)));
            Assert.That(tomorrow.Note, Does.Contain("Sample"));
        });
    }

    [TestCase("haftalık", "2026-01-01", 0, "2025-12-29", "2026-01-04", 7)]
    [TestCase("WEEKLY", "2026-11-08", 1, "2026-11-09", "2026-11-15", 7)]
    [TestCase("AYLIK", "2028-01-31", 1, "2028-02-01", "2028-02-29", 29)]
    [TestCase("month", "2026-01-31", -1, "2025-12-01", "2025-12-31", 31)]
    [TestCase("GÜNLÜK", "2026-12-31", 1, "2027-01-01", "2027-01-01", 1)]
    public async Task CalendarPeriods_IncludeEveryDayAcrossMonthAndYearBoundaries(string period, string date, int offset, string start, string end, int count)
    {
        var result = await handler.HandleAsync(new() { Period = period, Date = DateOnly.Parse(date), Offset = offset });
        Assert.Multiple(() =>
        {
            Assert.That(result.StartDate, Is.EqualTo(DateOnly.Parse(start)));
            Assert.That(result.EndDate, Is.EqualTo(DateOnly.Parse(end)));
            Assert.That(result.Data.Select(food => food.Date), Is.EqualTo(Enumerable.Range(0, count).Select(i => DateOnly.Parse(start).AddDays(i))));
            Assert.That(result.Total, Is.EqualTo(count));
            Assert.That(result.PageNumber, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(count));
        });
    }

    [Test]
    public async Task SameDate_HasConsistentMenuInAllPeriods_AndLocalizedDishNames()
    {
        var date = new DateOnly(2026, 11, 2);
        var daily = await handler.HandleAsync(new() { Date = date });
        var weekly = await handler.HandleAsync(new() { Date = date, Period = "week" });
        var monthly = await handler.HandleAsync(new() { Date = date, Period = "month" });
        var english = await handler.HandleAsync(new() { Date = date, Language = "en-US" });
        Assert.Multiple(() =>
        {
            Assert.That(JsonSerializer.Serialize(weekly.Data.Single(food => food.Date == date)), Is.EqualTo(JsonSerializer.Serialize(daily.Data.Single())));
            Assert.That(JsonSerializer.Serialize(monthly.Data.Single(food => food.Date == date)), Is.EqualTo(JsonSerializer.Serialize(daily.Data.Single())));
            Assert.That(daily.Data.Single().Soup, Is.EqualTo("Mercimek çorbası"));
            Assert.That(english.Data.Single().Soup, Is.EqualTo("Lentil soup"));
            Assert.That(english.Data.Single().MainCourse, Is.EqualTo("Grilled chicken"));
            Assert.That(english.Data.Single().SaladsAndAppetizers, Is.EqualTo(new[] { "Seasonal salad", "Hummus" }));
            Assert.That(english.Data.Single().DessertsAndFruits, Is.EqualTo(new[] { "Rice pudding", "Apple" }));
        });
    }

    [TestCase("year", "tr", 0)]
    [TestCase("", "tr", 0)]
    [TestCase(null, "tr", 0)]
    [TestCase("day", "de", 0)]
    [TestCase("day", null, 0)]
    [TestCase("day", "en", 367)]
    [TestCase("week", "tr", -367)]
    public void InvalidParameters_AreRejected(string? period, string? language, int offset) =>
        Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new() { Period = period!, Language = language!, Offset = offset }));

    [Test]
    public void CalendarOverflowAndCancellation_AreHandled()
    {
        Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new() { Date = DateOnly.MaxValue, Offset = 1 }));
        Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new() { Date = DateOnly.MaxValue, Period = "week" }));
        Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new() { Date = DateOnly.MinValue, Period = "month", Offset = -1 }));
        Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(new(), new CancellationToken(true)));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 1, 22, 30, 0, TimeSpan.Zero);
    }
}
