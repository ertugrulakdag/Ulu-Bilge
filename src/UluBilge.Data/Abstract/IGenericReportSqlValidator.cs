using UluBilge.Core.Models;

namespace UluBilge.Data.Abstract
{
    public interface IGenericReportSqlValidator
    {
        void Validate(string sql, IReadOnlyCollection<string> parameterNames, DatabaseSchemaSnapshot schema);
    }
}
