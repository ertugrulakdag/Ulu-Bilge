using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using UluBilge.Core.Enums;
using UluBilge.Core.Models;
using UluBilge.Data.Abstract;

namespace UluBilge.Data.Database
{
    public sealed class DatabaseSchemaProvider(DatabaseConnectionSettings settings, GenericReportOptions options) : IDatabaseSchemaProvider, IDisposable
    {
        private readonly SemaphoreSlim initializationLock = new(1, 1);
        private DatabaseSchemaSnapshot? snapshot;

        public DatabaseSchemaSnapshot Snapshot => Volatile.Read(ref snapshot)
            ?? throw new InvalidOperationException("Veritabanı şeması henüz hazırlanmadı. Generic rapor şu anda çalıştırılamaz.");

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await initializationLock.WaitAsync(ct);
            try
            {
                if (snapshot is not null)
                    return;

                const string sql = @"
                DECLARE @CanReadViewMetadata bit = CASE
                    WHEN HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DEFINITION') = 1
                        AND HAS_PERMS_BY_NAME(N'sys.sql_expression_dependencies', N'OBJECT', N'SELECT') = 1
                    THEN 1 ELSE 0 END;

                SELECT
                    o.object_id AS ObjectId,
                    s.name AS SchemaName,
                    o.name AS TableName,
                    CASE WHEN o.type = 'U' THEN 1 ELSE 2 END AS TableOrViewType,
                    c.column_id AS ColumnId,
                    c.name AS ColumnName,
                    TYPE_NAME(c.system_type_id) AS DataType,
                    c.is_nullable AS IsNullable,
                    CAST(pk.key_ordinal AS int) AS PrimaryKeyOrder,
                    CAST(c.max_length AS int) AS MaxLength,
                    c.precision AS Precision,
                    c.scale AS Scale
                FROM sys.objects o
                LEFT JOIN sys.tables t ON t.object_id = o.object_id
                INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
                INNER JOIN sys.columns c ON c.object_id = o.object_id
                LEFT JOIN (
                    SELECT ic.object_id, ic.column_id, ic.key_ordinal
                    FROM sys.indexes i
                    INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                    WHERE i.is_primary_key = 1 AND ic.key_ordinal > 0
                ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
                WHERE o.is_ms_shipped = 0 AND o.type IN ('U', 'V')
                    AND ((o.type = 'U' AND t.temporal_type <> 1 AND t.is_external = 0
                        AND NOT EXISTS (SELECT 1 FROM sys.computed_columns computed WHERE computed.object_id = o.object_id))
                        OR (o.type = 'V' AND @CanReadViewMetadata = 1))
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.columns clrColumn
                        INNER JOIN sys.types clrType ON clrType.user_type_id = clrColumn.user_type_id
                        WHERE clrColumn.object_id = o.object_id AND clrType.is_assembly_type = 1)
                    AND HAS_PERMS_BY_NAME(QUOTENAME(s.name) + N'.' + QUOTENAME(o.name), N'OBJECT', N'SELECT') = 1
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.columns deniedColumn
                        WHERE deniedColumn.object_id = o.object_id
                            AND ISNULL(HAS_PERMS_BY_NAME(QUOTENAME(s.name) + N'.' + QUOTENAME(o.name), N'OBJECT', N'SELECT', deniedColumn.name, N'COLUMN'), 0) = 0)
                    AND c.is_hidden = 0
                ORDER BY o.object_id, c.column_id;

                SELECT
                    fk.object_id AS ForeignKeyId,
                    fk.name AS Name,
                    source.object_id AS SourceObjectId,
                    sourceColumn.name AS SourceColumnName,
                    target.object_id AS TargetObjectId,
                    targetColumn.name AS TargetColumnName,
                    fkc.constraint_column_id AS ColumnOrder
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                INNER JOIN sys.tables source ON source.object_id = fk.parent_object_id
                INNER JOIN sys.columns sourceColumn ON sourceColumn.object_id = source.object_id AND sourceColumn.column_id = fkc.parent_column_id
                INNER JOIN sys.tables target ON target.object_id = fk.referenced_object_id
                INNER JOIN sys.columns targetColumn ON targetColumn.object_id = target.object_id AND targetColumn.column_id = fkc.referenced_column_id
                WHERE sourceColumn.is_hidden = 0 AND targetColumn.is_hidden = 0
                ORDER BY fk.object_id, fkc.constraint_column_id;

                SELECT v.object_id AS ObjectId, v.Name, m.definition AS Definition, m.uses_quoted_identifier AS UsesQuotedIdentifiers
                FROM sys.views v
                INNER JOIN sys.sql_modules m ON m.object_id = v.object_id
                WHERE v.is_ms_shipped = 0 AND m.definition IS NOT NULL AND @CanReadViewMetadata = 1
                ORDER BY v.object_id;

                SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'));
                SELECT @CanReadViewMetadata;

                IF @CanReadViewMetadata = 1
                BEGIN
                    SELECT d.referencing_id AS ViewObjectId, d.referenced_id AS ReferencedObjectId,
                        CAST(d.referenced_class AS int) AS ReferencedClass,
                        d.referenced_server_name AS ServerName, d.referenced_database_name AS DatabaseName,
                        s.name AS SchemaName, o.name AS ObjectName,
                        d.is_ambiguous AS IsAmbiguous, d.is_caller_dependent AS IsCallerDependent
                    FROM sys.sql_expression_dependencies d
                    INNER JOIN sys.views v ON v.object_id = d.referencing_id
                    LEFT JOIN sys.objects o ON d.referenced_class = 1 AND o.object_id = d.referenced_id
                    LEFT JOIN sys.schemas s ON s.schema_id = o.schema_id
                    WHERE v.is_ms_shipped = 0
                    ORDER BY d.referencing_id, d.referenced_id, d.referenced_minor_id;
                END;";

                await using var connection = new SqlConnection(settings.ReadConnection);
                await connection.OpenAsync(ct);
                using var results = await connection.QueryMultipleAsync(new CommandDefinition(sql,
                    commandTimeout: options.TimeoutSeconds, cancellationToken: ct));
                var columns = (await results.ReadAsync<ColumnRow>()).ToArray();
                var foreignKeys = (await results.ReadAsync<ForeignKeyRow>()).ToArray();
                var viewDefinitions = (await results.ReadAsync<ViewDefinitionRow>()).ToArray();
                var collation = await results.ReadSingleAsync<string>();
                var canReadViewMetadata = await results.ReadSingleAsync<bool>();
                var viewDependencies = canReadViewMetadata ? (await results.ReadAsync<ViewDependencyRow>()).ToArray() : [];

                // Dışlama hem tablolar hem view'ler için tek yerde uygulanır.
                var excluded = options.ExcludedTables.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var candidates = columns
                    .Where(column => !excluded.Contains(column.SchemaName + "." + column.TableName))
                    .GroupBy(column => column.ObjectId)
                    .ToDictionary(group => group.Key, group => group.OrderBy(column => column.ColumnId).ToArray());
                var allowedTables = GetAllowedObjects(candidates, viewDefinitions, viewDependencies);
                var tables = allowedTables.Values
                    .OrderBy(table => table[0].SchemaName, StringComparer.Ordinal)
                    .ThenBy(table => table[0].TableName, StringComparer.Ordinal)
                    .Select(table => CreateTable(table, allowedTables, foreignKeys))
                    .ToArray();
                // View tanımı aynı sütunlarla değişse bile yeniden başlangıçta sürüm değişir.
                var definitions = viewDefinitions.Where(view => allowedTables.ContainsKey(view.ObjectId))
                    .OrderBy(view => candidates[view.ObjectId][0].SchemaName, StringComparer.Ordinal)
                    .ThenBy(view => candidates[view.ObjectId][0].TableName, StringComparer.Ordinal)
                    .Select(view => new
                    {
                        candidates[view.ObjectId][0].SchemaName, candidates[view.ObjectId][0].TableName,
                        view.Definition, view.UsesQuotedIdentifiers,
                        Sources = viewDependencies.Where(dependency => dependency.ViewObjectId == view.ObjectId)
                            .Select(dependency => (dependency.SchemaName, dependency.ObjectName)).Distinct()
                            .OrderBy(source => source.SchemaName, StringComparer.Ordinal).ThenBy(source => source.ObjectName, StringComparer.Ordinal)
                            .Select(source => new { source.SchemaName, source.ObjectName }).ToArray()
                    }).ToArray();
                var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Collation = collation, Tables = tables, Views = definitions }))));
                var initialized = new DatabaseSchemaSnapshot
                {
                    Version = version,
                    LoadedAtUtc = DateTimeOffset.UtcNow,
                    Collation = collation,
                    Tables = Array.AsReadOnly(tables)
                };
                Volatile.Write(ref snapshot, initialized);
            }
            finally
            {
                initializationLock.Release();
            }
        }

        private static Dictionary<int, ColumnRow[]> GetAllowedObjects(Dictionary<int, ColumnRow[]> candidates,
            ViewDefinitionRow[] definitions, ViewDependencyRow[] dependencies)
        {
            var allowed = candidates.Where(pair => pair.Value[0].TableOrViewType == TableOrViewEnum.Table)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var pending = definitions.Where(view => candidates.ContainsKey(view.ObjectId)).ToDictionary(view => view.ObjectId);
            var sources = dependencies.ToLookup(dependency => dependency.ViewObjectId);
            while (pending.Count > 0)
            {
                // Yalnızca tüm kaynakları önceden doğrulanmış view'leri ekle.
                // Döngüler, çözülemeyen adlar, UDF, synonym ve dış veritabanları bu kümeye giremez.
                var ready = pending.Values.Where(view => sources[view.ObjectId].All(dependency =>
                    dependency.ReferencedClass == 1 && dependency.ServerName is null && dependency.DatabaseName is null
                    && !dependency.IsAmbiguous && !dependency.IsCallerDependent
                    && dependency.ReferencedObjectId is int id && allowed.ContainsKey(id))).ToArray();
                if (ready.Length == 0)
                    break;
                foreach (var view in ready)
                {
                    pending.Remove(view.ObjectId);
                    var names = sources[view.ObjectId].Select(dependency => (dependency.SchemaName!, dependency.ObjectName!)).Distinct().ToArray();
                    try
                    {
                        // Metadata tek başına OPENQUERY, oturum fonksiyonları veya hints gibi yapıları engellemez.
                        GenericReportSqlValidator.ValidateViewDefinition(view.Definition, view.UsesQuotedIdentifiers, names);
                    }
                    catch (ValidationException ex)
                    {
                        Console.WriteLine($"Validation failed for view {view.Name}: {ex.Message}"); 
                        continue;
                    }
                    allowed.Add(view.ObjectId, candidates[view.ObjectId]);
                }
            }
            return allowed;
        }

        private static DatabaseTableSchema CreateTable(ColumnRow[] table, IReadOnlyDictionary<int, ColumnRow[]> allowedTables, ForeignKeyRow[] foreignKeys)
        {
            var firstColumn = table[0];
            var columns = table.Select(column => new DatabaseColumnSchema
            {
                Name = column.ColumnName,
                DataType = column.DataType,
                IsNullable = column.IsNullable,
                PrimaryKeyOrder = column.PrimaryKeyOrder,
                MaxLength = column.MaxLength,
                Precision = column.Precision,
                Scale = column.Scale
            }).ToArray();
            var keys = foreignKeys
                .Where(key => key.SourceObjectId == firstColumn.ObjectId && allowedTables.ContainsKey(key.TargetObjectId))
                .GroupBy(key => key.ForeignKeyId)
                .OrderBy(group => group.First().Name, StringComparer.Ordinal)
                .Select(group =>
                {
                    var parts = group.OrderBy(key => key.ColumnOrder).ToArray();
                    var referencedTable = allowedTables[parts[0].TargetObjectId][0];
                    return new DatabaseForeignKeySchema
                    {
                        Name = parts[0].Name,
                        Columns = Array.AsReadOnly(parts.Select(key => key.SourceColumnName).ToArray()),
                        ReferencedSchemaName = referencedTable.SchemaName,
                        ReferencedTableName = referencedTable.TableName,
                        ReferencedColumns = Array.AsReadOnly(parts.Select(key => key.TargetColumnName).ToArray())
                    };
                }).ToArray();
            return new DatabaseTableSchema
            {
                SchemaName = firstColumn.SchemaName,
                TableName = firstColumn.TableName,
                TableOrViewType = firstColumn.TableOrViewType,
                Columns = Array.AsReadOnly(columns),
                ForeignKeys = Array.AsReadOnly(keys)
            };
        }

        private sealed class ColumnRow
        {
            public int ObjectId { get; set; }
            public string SchemaName { get; set; } = string.Empty;
            public string TableName { get; set; } = string.Empty;
            public TableOrViewEnum TableOrViewType { get; set; }
            public int ColumnId { get; set; }
            public string ColumnName { get; set; } = string.Empty;
            public string DataType { get; set; } = string.Empty;
            public bool IsNullable { get; set; }
            public int? PrimaryKeyOrder { get; set; }
            public int MaxLength { get; set; }
            public byte Precision { get; set; }
            public byte Scale { get; set; }
        }

        private sealed class ViewDefinitionRow
        {
            public int ObjectId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Definition { get; set; } = string.Empty;
            public bool UsesQuotedIdentifiers { get; set; }
        }

        private sealed class ViewDependencyRow
        {
            public int ViewObjectId { get; set; }
            public int? ReferencedObjectId { get; set; }
            public int ReferencedClass { get; set; }
            public string? ServerName { get; set; }
            public string? DatabaseName { get; set; }
            public string? SchemaName { get; set; }
            public string? ObjectName { get; set; }
            public bool IsAmbiguous { get; set; }
            public bool IsCallerDependent { get; set; }
        }

        private sealed class ForeignKeyRow
        {
            public int ForeignKeyId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int SourceObjectId { get; set; }
            public string SourceColumnName { get; set; } = string.Empty;
            public int TargetObjectId { get; set; }
            public string TargetColumnName { get; set; } = string.Empty;
            public int ColumnOrder { get; set; }
        }

        public void Dispose() => initializationLock.Dispose();
    }
}
