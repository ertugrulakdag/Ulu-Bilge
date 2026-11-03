using System.Buffers;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using UluBilge.Core.Models;
using UluBilge.Data.Abstract;
using UluBilge.Data.Database;

namespace UluBilge.Data.Repository
{
    public sealed class GenericReportRepository(IUnitOfWork unitOfWork, IDatabaseSchemaProvider schemaProvider, IGenericReportSqlValidator sqlValidator, GenericReportOptions options) : IGenericReportRepository
    {
        public async Task<GenericReportData> GetReportAsync(string sql, string sVersion, IReadOnlyList<GenericReportParameter> parameters, int maxRows, CancellationToken ct = default)
        {
            var schema = schemaProvider.Snapshot;
            if (!string.Equals(schema.Version, sVersion, StringComparison.Ordinal))
            {
                throw new ValidationException("Veritabanı şeması değişti. get_database_schema ile güncel şemayı alıp sorguyu yeniden hazırlayın.");
            }
            if (maxRows < 1 || maxRows > options.MaxRows)
            {
                throw new ValidationException($"Satır sınırı 1 ile {options.MaxRows} arasında olmalıdır.");
            }
            if (parameters is null || parameters.Count > 100 || parameters.Any(parameter => parameter is null))
            {
                throw new ValidationException("En fazla 100 geçerli SQL parametresi gönderilebilir.");
            }

            await using var command = unitOfWork.Connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = options.TimeoutSeconds;
            command.Transaction = unitOfWork.Transaction;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var input in parameters)
            {
                if (string.IsNullOrWhiteSpace(input.Name))
                {
                    throw new ValidationException("SQL parametre adı boş olamaz.");
                }
                var name = input.Name.StartsWith('@') ? input.Name : "@" + input.Name;
                if (!Regex.IsMatch(name, "^@[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant) || !names.Add(name))
                {
                    throw new ValidationException("SQL parametre adları geçerli ve benzersiz olmalıdır.");
                }
                var (dbType, value) = ConvertParameter(input);
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.DbType = dbType;
                parameter.Value = value ?? DBNull.Value;
                if (value is string text)
                {
                    parameter.Size = Math.Max(1, text.Length);
                }
                command.Parameters.Add(parameter);
            }

            sqlValidator.Validate(sql, names, schema);
            var requestToken = ct;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            ct = deadline.Token;
            // Reader kapanırken kalan verinin tüketilmesi de toplam süreyi aşmasın.
            using var cancellation = ct.Register(static state =>
            {
                try { ((DbCommand)state!).Cancel(); }
                catch (DbException) { }
                catch (InvalidOperationException) { }
            }, command);

            try
            {
                await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult, ct);
                if (reader.FieldCount > 128)
                {
                    throw new ValidationException("Bir rapor en fazla 128 sütun döndürebilir.");
                }
                var columns = new List<GenericReportColumn>();
                var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                {
                    var name = reader.GetName(ordinal);
                    if (string.IsNullOrWhiteSpace(name) || !columnNames.Add(name))
                    {
                        throw new ValidationException("Her sonuç sütununa benzersiz bir ad veya AS takma adı verin.");
                    }
                    columns.Add(new(name, reader.GetDataTypeName(ordinal)));
                }

                var bytes = JsonSerializer.SerializeToUtf8Bytes(columns).Length + 256;
                if (bytes >= options.MaxResultBytes)
                {
                    throw new ValidationException("Sütun bilgileri rapor boyut sınırını aşıyor. Daha az sütun seçin.");
                }
                var fieldNameBytes = columns.Select(column => JsonSerializer.SerializeToUtf8Bytes(column.Name).Length + 2).ToArray();
                var rows = new List<IReadOnlyDictionary<string, object?>>();
                var truncated = false;
                while (await reader.ReadAsync(ct))
                {
                    if (rows.Count >= maxRows)
                    {
                        truncated = true;
                        break;
                    }
                    var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                    var rowBytes = 2;
                    for (var ordinal = 0; ordinal < columns.Count; ordinal++)
                    {
                        var remainingBytes = options.MaxResultBytes - bytes - rowBytes - fieldNameBytes[ordinal] - 1;
                        if (remainingBytes < 4)
                        {
                            truncated = true;
                            break;
                        }
                        if (await reader.IsDBNullAsync(ordinal, ct))
                        {
                            row[columns[ordinal].Name] = null;
                            rowBytes += fieldNameBytes[ordinal] + 4;
                            continue;
                        }
                        var type = reader.GetFieldType(ordinal);
                        if (string.Equals(reader.GetDataTypeName(ordinal), "xml", StringComparison.OrdinalIgnoreCase))
                            throw new ValidationException("XML sütunlarını CAST(... AS nvarchar(max)) ile metne dönüştürün.");
                        // Uzunluğu önceden bilinmeyen LOB akışlarını da kalan bütçede oku.
                        object? value;
                        if (type == typeof(string))
                        {
                            value = await ReadTextAsync(reader, ordinal, remainingBytes - 2, ct);
                        }
                        else if (type == typeof(byte[]) && columns[ordinal].DataType is not ("timestamp" or "rowversion"))
                        {
                            value = await ReadBinaryAsync(reader, ordinal, (remainingBytes - 2) / 4 * 3, ct);
                        }
                        else
                        {
                            value = await reader.GetFieldValueAsync<object>(ordinal, ct);
                        }
                        if (value is null)
                        {
                            truncated = true;
                            break;
                        }
                        if (value is not (string or byte[] or bool or byte or short or int or long or decimal or float or double or DateTime or DateTimeOffset or TimeSpan or Guid))
                        {
                            throw new ValidationException("Raporun desteklenmeyen sütun tipini CAST ile metin veya sayıya dönüştürün.");
                        }
                        var valueBytes = JsonSerializer.SerializeToUtf8Bytes(value).Length;
                        if (valueBytes > remainingBytes)
                        {
                            truncated = true;
                            break;
                        }
                        rowBytes += fieldNameBytes[ordinal] + valueBytes;
                        row[columns[ordinal].Name] = value;
                    }
                    if (truncated)
                    {
                        break;
                    }
                    bytes += rowBytes + 1;
                    rows.Add(new ReadOnlyDictionary<string, object?>(row));
                }
                if (truncated)
                {
                    command.Cancel();
                }

                return new GenericReportData
                {
                    SVersion = schema.Version,
                    Columns = columns.AsReadOnly(),
                    Rows = rows.AsReadOnly(),
                    IsTruncated = truncated
                };
            }
            catch (OperationCanceledException) when (!requestToken.IsCancellationRequested && deadline.IsCancellationRequested)
            {
                throw new ValidationException("Rapor sorgusu süre sınırını aştı. Filtreleri daraltın.");
            }
            catch (SqlException exception)
            {
                requestToken.ThrowIfCancellationRequested();
                if (deadline.IsCancellationRequested)
                {
                    throw new ValidationException("Rapor sorgusu süre sınırını aştı. Filtreleri daraltın.");
                }
                throw new ValidationException(exception.Number switch
                {
                    -2 => "Rapor sorgusu süre sınırını aştı. Filtreleri daraltın.",
                    207 or 208 or 209 or 4104 => "Sorgudaki tablo, sütun veya takma ad geçersiz. get_database_schema sonucuna göre sorguyu düzeltin.",
                    229 or 230 => "ReadConnection hesabının bu raporu okuma yetkisi yok.",
                    _ => "Rapor SQL'i çalıştırılamadı. Sütun tiplerini, ilişkileri ve parametreleri kontrol edin."
                });
            }
            catch (OverflowException)
            {
                throw new ValidationException("Sayısal değer desteklenen aralığı aşıyor. SQL'de uygun bir CAST kullanın.");
            }
        }

        private static async Task<string?> ReadTextAsync(DbDataReader reader, int ordinal, int maxCharacters, CancellationToken ct)
        {
            using var text = reader.GetTextReader(ordinal);
            var buffer = ArrayPool<char>.Shared.Rent(4096);
            try
            {
                var result = new StringBuilder();
                while (true)
                {
                    var count = await text.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, maxCharacters - result.Length + 1)), ct);
                    if (count == 0)
                    {
                        return result.ToString();
                    }
                    if (result.Length + count > maxCharacters)
                    {
                        return null;
                    }
                    result.Append(buffer, 0, count);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer, clearArray: true);
            }
        }

        private static async Task<byte[]?> ReadBinaryAsync(DbDataReader reader, int ordinal, int maxBytes, CancellationToken ct)
        {
            await using var stream = reader.GetStream(ordinal);
            using var result = new MemoryStream();
            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                while (true)
                {
                    var count = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, maxBytes - result.Length + 1)), ct);
                    if (count == 0)
                    {
                        return result.ToArray();
                    }
                    if (result.Length + count > maxBytes)
                    {
                        return null;
                    }
                    result.Write(buffer, 0, count);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }

        private static (DbType Type, object? Value) ConvertParameter(GenericReportParameter parameter)
        {
            if (!Enum.IsDefined(parameter.Type) || parameter.Value.ValueKind == JsonValueKind.Undefined)
            {
                throw new ValidationException("Her SQL parametresi için type ve value belirtin; boş değer için null kullanın.");
            }
            var type = parameter.Type switch
            {
                GenericReportParameterType.String => DbType.String,
                GenericReportParameterType.Int32 => DbType.Int32,
                GenericReportParameterType.Int64 => DbType.Int64,
                GenericReportParameterType.Decimal => DbType.Decimal,
                GenericReportParameterType.Double => DbType.Double,
                GenericReportParameterType.Boolean => DbType.Boolean,
                GenericReportParameterType.Date => DbType.Date,
                GenericReportParameterType.DateTime => DbType.DateTime2,
                GenericReportParameterType.DateTimeOffset => DbType.DateTimeOffset,
                GenericReportParameterType.Guid => DbType.Guid,
                _ => throw new ValidationException("Geçersiz SQL parametre tipi.")
            };
            if (parameter.Value.ValueKind == JsonValueKind.Null)
            {
                return (type, null);
            }
            try
            {
                object? value = parameter.Type switch
                {
                    GenericReportParameterType.String => parameter.Value.GetString(),
                    GenericReportParameterType.Int32 => parameter.Value.GetInt32(),
                    GenericReportParameterType.Int64 => parameter.Value.GetInt64(),
                    GenericReportParameterType.Decimal => parameter.Value.GetDecimal(),
                    GenericReportParameterType.Double => parameter.Value.GetDouble(),
                    GenericReportParameterType.Boolean => parameter.Value.GetBoolean(),
                    GenericReportParameterType.Date => DateOnly.ParseExact(parameter.Value.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue),
                    GenericReportParameterType.DateTime => parameter.Value.GetDateTime(),
                    GenericReportParameterType.DateTimeOffset => parameter.Value.GetDateTimeOffset(),
                    GenericReportParameterType.Guid => parameter.Value.GetGuid(),
                    _ => throw new ValidationException("Geçersiz SQL parametre tipi.")
                };
                if (value is string { Length: > 4000 } || value is double number && !double.IsFinite(number))
                {
                    throw new ValidationException("SQL parametresi en fazla 4000 karakterlik metin veya sonlu bir sayı olmalıdır.");
                }
                return (type, value);
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException or ArgumentException)
            {
                throw new ValidationException("SQL parametre değeri belirtilen tipe uygun değil. Tarihleri ISO 8601 biçiminde gönderin.");
            }
        }
    }
}
