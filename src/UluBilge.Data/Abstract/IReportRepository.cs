using UluBilge.Core.Models;

namespace UluBilge.Data.Abstract
{
    public interface IReportRepository
    {
        Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesAsync(int? year, int topCount, CancellationToken ct = default);
        Task<IReadOnlyList<CustomerSalesReportItem>> GetCustomerSalesAsync(int? year, int topCount, CancellationToken ct = default);
        Task<IReadOnlyList<EmployeeSalesReportItem>> GetEmployeeSalesAsync(int? year, int topCount, CancellationToken ct = default);
    }
}
