using System.Text.Json;
using System.Text.Json.Serialization;

namespace UluBilge.Core.Models
{
    public sealed class GenericReportParameter
    {
        public required string Name { get; init; }
        public required GenericReportParameterType Type { get; init; }
        public required JsonElement Value { get; init; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<GenericReportParameterType>))]
    public enum GenericReportParameterType
    {
        String,
        Int32,
        Int64,
        Decimal,
        Double,
        Boolean,
        Date,
        DateTime,
        DateTimeOffset,
        Guid
    }

    public sealed record GenericReportColumn(string Name, string DataType);

    public sealed class GenericReportData
    {
        [JsonPropertyName("sVersion")]
        public string SVersion { get; init; } = string.Empty;
        public IReadOnlyList<GenericReportColumn> Columns { get; init; } = [];
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];
        public int ReturnedRowCount => Rows.Count;
        public bool IsTruncated { get; init; }
    }
}
