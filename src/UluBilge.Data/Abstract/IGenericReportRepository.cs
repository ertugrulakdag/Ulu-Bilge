using UluBilge.Core.Models;

namespace UluBilge.Data.Abstract
{
    public interface IGenericReportRepository
    {
        Task<GenericReportData> GetReportAsync(string sql, string sVersion, IReadOnlyList<GenericReportParameter> parameters, int maxRows, CancellationToken ct = default);
    }
}
