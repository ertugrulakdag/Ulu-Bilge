using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using UluBilge.Core.Models;
using UluBilge.Data.Abstract;

namespace UluBilge.Data.Database
{
    public sealed class GenericReportSqlValidator : IGenericReportSqlValidator
    {
        private const int MaximumSqlLength = 32_768;
        private const int MaximumNodeCount = 10_000;
        private const int MaximumDepth = 128;
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ChildProperties = new();

        // Tanınmayan düğümler reddedilir: yeni SQL sözdizimi kendiliğinden yetki kazanmaz.
        private static readonly HashSet<string> AllowedNodes = new(StringComparer.Ordinal)
        {
            "TSqlScript", "TSqlBatch", "SelectStatement", "WithCtesAndXmlNamespaces", "CommonTableExpression",
            "QuerySpecification", "QueryParenthesisExpression", "BinaryQueryExpression", "FromClause",
            "NamedTableReference", "QualifiedJoin", "UnqualifiedJoin", "JoinParenthesisTableReference",
            "QueryDerivedTable", "InlineDerivedTable", "RowValue", "SelectScalarExpression", "SelectStarExpression",
            "WhereClause", "HavingClause", "GroupByClause", "ExpressionGroupingSpecification",
            "CompositeGroupingSpecification", "CubeGroupingSpecification", "RollupGroupingSpecification",
            "GrandTotalGroupingSpecification", "GroupingSetsGroupingSpecification", "OrderByClause",
            "ExpressionWithSortOrder", "OffsetClause", "TopRowFilter", "WindowClause", "WindowDefinition",
            "OverClause", "WithinGroupClause", "WindowFrameClause", "WindowFrameDelimiter",
            "Identifier", "IdentifierOrValueExpression", "MultiPartIdentifier", "SchemaObjectName",
            "ColumnReferenceExpression", "VariableReference", "IntegerLiteral", "NumericLiteral", "RealLiteral",
            "MoneyLiteral", "StringLiteral", "NullLiteral", "BinaryLiteral", "MaxLiteral",
            "UnaryExpression", "BinaryExpression", "ParenthesisExpression", "ScalarSubquery", "FunctionCall",
            "CastCall", "ConvertCall", "TryCastCall", "TryConvertCall", "CoalesceExpression", "NullIfExpression",
            "IIfCall", "LeftFunctionCall", "RightFunctionCall", "AtTimeZoneCall", "ParameterlessCall",
            "SearchedCaseExpression", "SearchedWhenClause", "SimpleCaseExpression", "SimpleWhenClause",
            "SqlDataTypeReference", "BooleanBinaryExpression", "BooleanComparisonExpression",
            "BooleanParenthesisExpression", "BooleanNotExpression", "BooleanIsNullExpression",
            "BooleanTernaryExpression", "InPredicate", "LikePredicate", "ExistsPredicate",
            "SubqueryComparisonPredicate", "DistinctPredicate", "IdentifierLiteral"
        };

        private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
        {
            "COUNT", "COUNT_BIG", "SUM", "AVG", "MIN", "MAX", "STDEV", "STDEVP", "VAR", "VARP",
            "APPROX_COUNT_DISTINCT", "STRING_AGG", "GROUPING", "GROUPING_ID",
            "DATEADD", "DATEDIFF", "DATEDIFF_BIG", "DATEPART", "DATENAME", "YEAR", "MONTH", "DAY",
            "EOMONTH", "DATEFROMPARTS", "DATETIMEFROMPARTS", "DATETIME2FROMPARTS", "DATETIMEOFFSETFROMPARTS",
            "TIMEFROMPARTS", "SMALLDATETIMEFROMPARTS", "GETDATE", "GETUTCDATE", "SYSDATETIME",
            "SYSUTCDATETIME", "SYSDATETIMEOFFSET", "DATETRUNC", "DATE_BUCKET",
            "ABS", "CEILING", "FLOOR", "ROUND", "POWER", "SQRT", "SIGN", "EXP", "LOG", "LOG10",
            "SQUARE", "PI", "SIN", "COS", "TAN", "ATAN", "ATN2", "ACOS", "ASIN", "COT", "DEGREES", "RADIANS",
            "CONCAT", "CONCAT_WS", "LEFT", "RIGHT", "SUBSTRING", "LEN", "DATALENGTH", "TRIM", "LTRIM", "RTRIM",
            "UPPER", "LOWER", "REPLACE", "TRANSLATE", "STUFF", "CHARINDEX", "PATINDEX", "REVERSE",
            "ISNULL", "NULLIF", "COALESCE", "IIF", "CHOOSE",
            "ROW_NUMBER", "RANK", "DENSE_RANK", "NTILE", "LAG", "LEAD", "FIRST_VALUE", "LAST_VALUE",
            "CUME_DIST", "PERCENT_RANK", "PERCENTILE_CONT", "PERCENTILE_DISC",
            "JSON_VALUE", "JSON_QUERY", "JSON_PATH_EXISTS", "ISJSON", "STRING_ESCAPE"
        };

        public void Validate(string sql, IReadOnlyCollection<string> parameterNames, DatabaseSchemaSnapshot schema)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ParseAndValidate(sql, parameterNames, schema);
        }

        // Yalnızca metadata ile kimliği çözülmüş ve izinli olduğu kanıtlanmış kaynaklar gönderilir.
        internal static void ValidateViewDefinition(string definition, bool usesQuotedIdentifiers, IReadOnlyList<(string SchemaName, string TableName)> sources)
        {
            new GenericReportSqlValidator().ParseAndValidate(definition, [], null, sources, usesQuotedIdentifiers);
        }

        private void ParseAndValidate(string sql, IReadOnlyCollection<string> parameterNames, DatabaseSchemaSnapshot? schema,
            IReadOnlyList<(string SchemaName, string TableName)>? viewSources = null, bool usesQuotedIdentifiers = true)
        {
            ArgumentNullException.ThrowIfNull(parameterNames);
            if (string.IsNullOrWhiteSpace(sql) || sql.Length > MaximumSqlLength)
            {
                throw new ValidationException($"SQL boş olamaz ve en fazla {MaximumSqlLength} karakter olabilir.");
            }

            var suppliedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in parameterNames)
            {
                if (!IsParameterName(name) || !suppliedParameters.Add(name))
                {
                    throw new ValidationException("SQL parametre adları @ ile başlamalı, benzersiz olmalı ve yalnızca harf, rakam veya alt çizgi içermelidir.");
                }
            }

            var parser = new TSql160Parser(initialQuotedIdentifiers: usesQuotedIdentifiers);
            using var reader = new StringReader(sql);
            var tokens = parser.GetTokenStream(reader, out var tokenErrors);
            if (tokenErrors.Count > 0 || tokens.Count > MaximumNodeCount)
            {
                throw new ValidationException("SQL ayrıştırılamadı veya izin verilen karmaşıklık sınırını aşıyor.");
            }
            var parenthesisDepth = 0;
            var caseDepth = 0;
            var unaryDepth = 0;
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token.Text) || token.Text.StartsWith("--", StringComparison.Ordinal) || token.Text.StartsWith("/*", StringComparison.Ordinal))
                {
                    continue;
                }
                if (token.TokenType == TSqlTokenType.LeftParenthesis && ++parenthesisDepth > MaximumDepth)
                {
                    throw new ValidationException("SQL sorgusu izin verilen iç içe ifade sınırını aşıyor.");
                }
                if (token.TokenType == TSqlTokenType.RightParenthesis)
                {
                    parenthesisDepth--;
                }
                if (token.TokenType == TSqlTokenType.Case && ++caseDepth > MaximumDepth)
                {
                    throw new ValidationException("SQL sorgusu izin verilen CASE derinliğini aşıyor.");
                }
                if (token.TokenType == TSqlTokenType.End)
                {
                    caseDepth--;
                }
                if (token.TokenType == TSqlTokenType.Not || token.Text is "+" or "-" or "~")
                {
                    if (++unaryDepth > MaximumDepth)
                        throw new ValidationException("SQL sorgusu izin verilen işleç derinliğini aşıyor.");
                }
                else
                {
                    unaryDepth = 0;
                }
            }
            var fragment = parser.Parse(tokens, out var errors);
            if (errors.Count > 0)
            {
                throw new ValidationException($"SQL ayrıştırılamadı. Satır: {errors[0].Line}, sütun: {errors[0].Column}.");
            }

            if (fragment is not TSqlScript script || script.Batches.Count != 1 || script.TrailingGoCount != 0 || script.Batches[0].Statements.Count != 1)
            {
                throw new ValidationException("Generic rapor yalnızca tek bir SELECT sorgusu veya SELECT ile biten CTE içerebilir.");
            }

            var select = script.Batches[0].Statements[0] switch
            {
                SelectStatement statement when viewSources is null => statement,
                ViewStatementBody view when viewSources is not null => view.SelectStatement,
                _ => throw new ValidationException("Generic rapor yalnızca tek bir SELECT sorgusu veya SELECT ile biten CTE içerebilir.")
            };
            var allowedTables = schema?.Tables.Select(table => (table.SchemaName, table.TableName))
                .ToHashSet(new TableNameComparer());
            var commonTables = (select.WithCtesAndXmlNamespaces?.CommonTableExpressions ?? [])
                .Select(cte => cte.ExpressionName.Value).ToHashSet(StringComparer.Ordinal);
            var referencedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nodeCount = 0;
            Visit(select, 0);

            if (!suppliedParameters.SetEquals(referencedParameters))
            {
                throw new ValidationException("SQL içinde kullanılan parametrelerle gönderilen parametreler birebir eşleşmelidir.");
            }

            void Visit(TSqlFragment node, int depth)
            {
                if (depth > MaximumDepth || ++nodeCount > MaximumNodeCount)
                {
                    throw new ValidationException("SQL sorgusu izin verilen karmaşıklık sınırını aşıyor.");
                }

                if (!AllowedNodes.Contains(node.GetType().Name))
                {
                    throw new ValidationException($"Generic raporda desteklenmeyen SQL yapısı: {node.GetType().Name}.");
                }

                switch (node)
                {
                    case SelectStatement statement when statement.Into is not null || statement.On is not null || statement.OptimizerHints.Count > 0 || statement.ComputeClauses.Count > 0:
                        {
                            throw new ValidationException("SELECT INTO, dosya grubu, COMPUTE ve sorgu ipuçları raporlarda kullanılamaz.");
                        }
                    case WithCtesAndXmlNamespaces with when with.XmlNamespaces is not null || with.ChangeTrackingContext is not null:
                        {
                            throw new ValidationException("XML ad alanları ve değişiklik izleme bağlamı raporlarda kullanılamaz.");
                        }
                    case NamedTableReference table:
                        {
                            ValidateTable(table);
                            break;
                        }
                    case TableReferenceWithAlias table when table.ForPath:
                        {
                            throw new ValidationException("Graf yol başvuruları raporlarda kullanılamaz.");
                        }
                    case QualifiedJoin join when join.JoinHint != JoinHint.None:
                        {
                            throw new ValidationException("JOIN ipuçları raporlarda kullanılamaz.");
                        }
                    case FunctionCall function when function.CallTarget is not null || !AllowedFunctions.Contains(function.FunctionName.Value):
                        {
                            throw new ValidationException("Sorgu yalnızca raporlama için izin verilen yerleşik SQL fonksiyonlarını kullanabilir.");
                        }
                    case ParameterlessCall call when call.ParameterlessCallType.ToString() is not ("CurrentTimestamp" or "CurrentDate"):
                        {
                            throw new ValidationException("Oturum ve kullanıcı bilgisi fonksiyonları raporlarda kullanılamaz.");
                        }
                    case VariableReference variable:
                        {
                            if (!suppliedParameters.Contains(variable.Name))
                            {
                                throw new ValidationException("Sorguda tanımlanmamış SQL parametresi kullanılıyor.");
                            }
                            referencedParameters.Add(variable.Name);
                            break;
                        }
                    case ColumnReferenceExpression column when column.MultiPartIdentifier?.Identifiers.Count > 2 || column.ColumnType is not (ColumnType.Regular or ColumnType.Wildcard):
                        {
                            throw new ValidationException("Sütunlar adlarıyla veya tablo takma adıyla belirtilmelidir; özel sütunlar kullanılamaz.");
                        }
                    case SelectStarExpression star when star.Qualifier?.Identifiers.Count > 1:
                        {
                            throw new ValidationException("Yıldız sütun seçimi yalnızca tablo takma adıyla kullanılabilir.");
                        }
                }

                // ScriptTokenStream gibi metadata koleksiyonlarını değil, bütün AST çocuklarını dolaş.
                foreach (var property in ChildProperties.GetOrAdd(node.GetType(), FindChildProperties))
                {
                    var value = property.GetValue(node);
                    if (value is TSqlFragment child)
                    {
                        Visit(child, depth + 1);
                    }
                    else if (value is IEnumerable<TSqlFragment> children)
                    {
                        foreach (var item in children)
                        {
                            Visit(item, depth + 1);
                        }
                    }
                }
            }

            void ValidateTable(NamedTableReference table)
            {
                if (table.TableHints.Count > 0 || table.TableSampleClause is not null || table.TemporalClause is not null || table.ForPath)
                {
                    throw new ValidationException("Tablo ipuçları, örnekleme, zamansal sorgular ve graf yolları raporlarda kullanılamaz.");
                }

                var names = table.SchemaObject.Identifiers;
                if (names.Count == 1 && commonTables.Contains(names[0].Value))
                {
                    return;
                }

                var allowed = viewSources is null
                    ? names.Count == 2 && allowedTables!.Contains((names[0].Value, names[1].Value))
                    : names.Count is 1 or 2 && viewSources.Any(source =>
                        StringComparer.OrdinalIgnoreCase.Equals(source.TableName, names[^1].Value)
                        && (names.Count == 1 || StringComparer.OrdinalIgnoreCase.Equals(source.SchemaName, names[0].Value)));
                if (!allowed)
                {
                    throw new ValidationException("Sorgu yalnızca get_database_schema sonucundaki izinli tablo ve view'leri schema.Nesne biçiminde kullanabilir.");
                }
            }
        }

        private static PropertyInfo[] FindChildProperties(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0
                && (typeof(TSqlFragment).IsAssignableFrom(property.PropertyType)
                    || property.PropertyType.GetInterfaces().Append(property.PropertyType).Any(candidate => candidate.IsGenericType
                        && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                        && typeof(TSqlFragment).IsAssignableFrom(candidate.GetGenericArguments()[0]))))
            .ToArray();

        private static bool IsParameterName(string? name) => name is { Length: >= 2 and <= 129 } && name[0] == '@' && (char.IsAsciiLetter(name[1]) || name[1] == '_') && name.AsSpan(2).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_") < 0;

        private sealed class TableNameComparer : IEqualityComparer<(string Schema, string Table)>
        {
            public bool Equals((string Schema, string Table) x, (string Schema, string Table) y) => StringComparer.Ordinal.Equals(x.Schema, y.Schema) && StringComparer.Ordinal.Equals(x.Table, y.Table);

            public int GetHashCode((string Schema, string Table) obj) => HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.Schema), StringComparer.Ordinal.GetHashCode(obj.Table));
        }
    }
}
