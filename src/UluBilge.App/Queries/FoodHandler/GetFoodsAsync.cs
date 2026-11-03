using UluBilge.Core.Models;
using UluBilge.Core.Queries;
using System.ComponentModel.DataAnnotations;

namespace UluBilge.App.Queries.FoodHandler
{
    public static class GetFoodsAsync
    {
        public sealed class Query : IQuery<Result>, IValidatableObject
        {
            public string Period { get; set; } = "day";
            public DateOnly? Date { get; set; }

            [Range(-366, 366, ErrorMessage = "Offset -366 ile 366 arasında olmalıdır. / Offset must be between -366 and 366.")]
            public int Offset { get; set; }

            public string Language { get; set; } = "tr";

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (NormalizePeriod(Period) is null)
                    yield return new ValidationResult("Dönem day/günlük, week/haftalık veya month/aylık olmalıdır. / Period must be day, week or month.", [nameof(Period)]);

                if (NormalizeLanguage(Language) is null)
                    yield return new ValidationResult("Dil tr veya en olmalıdır. / Language must be tr or en.", [nameof(Language)]);
            }
        }

        public sealed class Result : PagedResult<Food>
        {
            public string Period { get; init; } = string.Empty;
            public DateOnly StartDate { get; init; }
            public DateOnly EndDate { get; init; }
            public string Language { get; init; } = string.Empty;
            public string TimeZone { get; init; } = "Turkey Standard Time";
            public bool IsSampleData { get; init; } = true;
            public string Note { get; init; } = string.Empty;
        }

        public sealed class Handler(TimeProvider clock) : IAsyncQueryHandler<Query, Result>
        {
            //private static readonly TimeZoneInfo CompanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            private static readonly TimeZoneInfo CompanyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");


            public Task<Result> HandleAsync(Query query, CancellationToken ct = default)
            {
                ArgumentNullException.ThrowIfNull(query);
                ct.ThrowIfCancellationRequested();
                Validator.ValidateObject(query, new ValidationContext(query), validateAllProperties: true);

                var period = NormalizePeriod(query.Period)!;
                var language = NormalizeLanguage(query.Language)!;
                var anchor = query.Date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), CompanyTimeZone).DateTime);
                DateOnly startDate;
                DateOnly endDate;
                try
                {
                    // Apply month offsets to the first day so month-end dates remain valid.
                    startDate = period switch
                    {
                        "week" => anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7)).AddDays(query.Offset * 7),
                        "month" => new DateOnly(anchor.Year, anchor.Month, 1).AddMonths(query.Offset),
                        _ => anchor.AddDays(query.Offset)
                    };
                    endDate = period switch
                    {
                        "week" => startDate.AddDays(6),
                        "month" => new DateOnly(startDate.Year, startDate.Month, DateTime.DaysInMonth(startDate.Year, startDate.Month)),
                        _ => startDate
                    };
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new ValidationException("Tarih ve offset desteklenen takvim aralığını aşıyor. / Date and offset exceed the supported calendar range.");
                }

                var count = endDate.DayNumber - startDate.DayNumber + 1;
                var foods = new List<Food>(count);
                for (var i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var date = startDate.AddDays(i);
                    // A date has the same sample menu in daily, weekly and monthly queries.
                    var menu = SampleMenus[date.DayNumber % SampleMenus.Length];
                    var localized = language == "en" ? menu.English : menu.Turkish;
                    foods.Add(new Food
                    {
                        Date = date,
                        // Keep sample calories consistent across languages and period queries for the same date.
                        TotalCalorie = new Random(date.DayNumber).Next(menu.MinCalorie, menu.MaxCalorie + 1),
                        Soup = localized.Soup,
                        MainCourse = localized.MainCourse,
                        SaladsAndAppetizers = localized.SaladsAndAppetizers.ToArray(),
                        DessertsAndFruits = localized.DessertsAndFruits.ToArray()
                    });
                }

                return Task.FromResult(new Result
                {
                    Data = foods,
                    Total = count,
                    PageNumber = 1,
                    PageSize = count,
                    Period = period,
                    StartDate = startDate,
                    EndDate = endDate,
                    Language = language,
                    Note = language == "en"
                        ? "Sample lunch menus generated for demonstration, including weekends."
                        : "Hafta sonları dahil, gösterim amacıyla üretilmiş örnek öğle yemeği menüleridir."
                });
            }
        }

        private static string? NormalizePeriod(string? period) => period?.Trim().ToLowerInvariant().Replace('ı', 'i') switch
        {
            "day" or "daily" or "gün" or "gun" or "günlük" or "gunluk" => "day",
            "week" or "weekly" or "hafta" or "haftalik" => "week",
            "month" or "monthly" or "ay" or "aylik" => "month",
            _ => null
        };

        private static string? NormalizeLanguage(string? language) => language?.Trim().ToLowerInvariant() switch
        {
            "tr" or "tr-tr" => "tr",
            "en" or "en-us" or "en-gb" => "en",
            _ => null
        };

        private sealed record Menu(string Soup, string MainCourse, string[] SaladsAndAppetizers, string[] DessertsAndFruits);

        private static readonly (Menu Turkish, Menu English, int MinCalorie, int MaxCalorie)[] SampleMenus =
        [
            (new("Mercimek çorbası", "Izgara tavuk", ["Mevsim salatası", "Humus"], ["Sütlaç", "Elma"]),
             new("Lentil soup", "Grilled chicken", ["Seasonal salad", "Hummus"], ["Rice pudding", "Apple"]), 750, 950),
            (new("Ezogelin çorbası", "Etli kuru fasulye", ["Çoban salata", "Cacık"], ["İrmik helvası", "Portakal"]),
             new("Ezogelin soup", "White bean and beef stew", ["Shepherd's salad", "Yogurt and cucumber dip"], ["Semolina halva", "Orange"]), 800, 1050),
            (new("Domates çorbası", "Fırında köfte", ["Yeşil salata", "Haydari"], ["Revani", "Armut"]),
             new("Tomato soup", "Baked meatballs", ["Green salad", "Herbed yogurt dip"], ["Semolina syrup cake", "Pear"]), 850, 1100),
            (new("Yayla çorbası", "Sebzeli tavuk sote", ["Gavurdağı salatası", "Zeytinyağlı enginar"], ["Kazandibi", "Mandalina"]),
             new("Yogurt and rice soup", "Chicken and vegetable saute", ["Tomato and walnut salad", "Artichokes in olive oil"], ["Caramelized milk pudding", "Mandarin"]), 700, 950),
            (new("Sebze çorbası", "Nohut yemeği", ["Piyaz", "Közlenmiş biber"], ["Baklava", "Muz"]),
             new("Vegetable soup", "Chickpea stew", ["White bean salad", "Roasted peppers"], ["Baklava", "Banana"]), 900, 1200),
            (new("Tarhana çorbası", "Izgara balık", ["Roka salatası", "Fava"], ["Tahin helvası", "Elma"]),
             new("Tarhana soup", "Grilled fish", ["Arugula salad", "Fava bean puree"], ["Tahini halva", "Apple"]), 750, 1000),
            (new("Mantar çorbası", "Sebzeli güveç", ["Bulgur salatası", "Yoğurt"], ["Keşkül", "Armut"]),
             new("Mushroom soup", "Vegetable casserole", ["Bulgur salad", "Yogurt"], ["Almond milk pudding", "Pear"]), 650, 850)
        ];
    }

}
