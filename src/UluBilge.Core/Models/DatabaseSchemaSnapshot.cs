using System.Text.Json.Serialization;
using UluBilge.Core.Enums;

namespace UluBilge.Core.Models
{
    public sealed record DatabaseSchemaSnapshot
    {
        public required string Version { get; init; }
        public required DateTimeOffset LoadedAtUtc { get; init; }
        public required string Collation { get; init; }
        public required IReadOnlyList<DatabaseTableSchema> Tables { get; init; }
    }

    public sealed record DatabaseTableSchema
    {
        public required string SchemaName { get; init; }
        public required string TableName { get; init; }
        [JsonConverter(typeof(JsonNumberEnumConverter<TableOrViewEnum>))]
        public required TableOrViewEnum TableOrViewType { get; init; }
        public required IReadOnlyList<DatabaseColumnSchema> Columns { get; init; }
        public required IReadOnlyList<DatabaseForeignKeySchema> ForeignKeys { get; init; }
    }

    public sealed record DatabaseColumnSchema
    {
        public required string Name { get; init; }
        public required string DataType { get; init; }
        public required bool IsNullable { get; init; }
        public int? PrimaryKeyOrder { get; init; }

        // SQL Server metadata uzunluğu bayt cinsindendir; -1, MAX anlamına gelir.
        public required int MaxLength { get; init; }
        public required byte Precision { get; init; }
        public required byte Scale { get; init; }
    }

    public sealed record DatabaseForeignKeySchema
    {
        public required string Name { get; init; }
        public required IReadOnlyList<string> Columns { get; init; }
        public required string ReferencedSchemaName { get; init; }
        public required string ReferencedTableName { get; init; }
        public required IReadOnlyList<string> ReferencedColumns { get; init; }
    }
}
