using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UluBilge.Core.Models;
using UluBilge.Core.Queries;
using UluBilge.Data.Abstract;
using UluBilge.Data.Database;

namespace UluBilge.App.Queries.ReportHandler
{
    public static class GetGenericReportAsync
    {
        public sealed class Query : IQuery<Result>, IValidatableObject
        {
            [Required, StringLength(32768)]
            public string Sql { get; init; } = string.Empty;

            [Required, StringLength(64, MinimumLength = 64)]
            [JsonPropertyName("sVersion")]
            public string SVersion { get; init; } = string.Empty;

            public IReadOnlyList<GenericReportParameter> Parameters { get; init; } = [];

            [Range(1, 10000)]
            public int? MaxRows { get; init; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (Parameters is null || Parameters.Count > 100 || Parameters.Any(parameter => parameter is null))
                    yield return new ValidationResult("En fazla 100 geçerli SQL parametresi gönderilebilir.", [nameof(Parameters)]);
            }
        }

        public sealed class Result
        {
            [JsonPropertyName("sVersion")]
            public string SVersion { get; init; } = string.Empty;
            public IReadOnlyList<GenericReportColumn> Columns { get; init; } = [];
            public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];
            public int ReturnedRowCount => Rows.Count;
            public bool IsTruncated { get; init; }
        }

        public sealed class Handler(IGenericReportRepository genericReportRepository, GenericReportOptions options) : IAsyncQueryHandler<Query, Result>
        {
            public async Task<Result> HandleAsync(Query query, CancellationToken ct = default)
            {
                var report = await genericReportRepository.GetReportAsync(query.Sql, query.SVersion, query.Parameters, query.MaxRows ?? Math.Min(200, options.MaxRows), ct);
                return new Result
                {
                    SVersion = report.SVersion,
                    Columns = report.Columns,
                    Rows = report.Rows,
                    IsTruncated = report.IsTruncated
                };
            }
        }
    }
}
