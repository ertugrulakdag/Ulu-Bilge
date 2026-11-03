using Dapper;
using UluBilge.Core.Models;
using UluBilge.Data.Abstract;

namespace UluBilge.Data.Repository
{
    public sealed class ReportRepository(IUnitOfWork unitOfWork) : IReportRepository
    {
        /// <summary>
        /// Northwind siparişlerinden ürün bazında satış raporu oluşturur ve indirim sonrası net satış tutarı en yüksek ürünleri listeler. Her ürün için ürün numarası ve adı, toplam satılan adet, sipariş sayısı ve net satış tutarı döndürülür.
        /// </summary>
        /// <remarks>
        /// Net satış, sipariş detaylarındaki UnitPrice × Quantity × (1 - Discount) tutarlarının toplamıdır ve iki ondalığa yuvarlanır; kargo bedeli dahil değildir. Sonuçlar net satış tutarına göre azalan, eşit tutarlarda ürün numarasına göre artan sırada döner. Satışı olmayan ürünler rapora dahil edilmez.
        /// </remarks>
        /// <param name="year">Sipariş tarihine (Orders.OrderDate) göre filtrelenecek yıl (1753–9999). Null ise tüm yıllar alınır.</param>
        /// <param name="topCount">Döndürülecek en fazla ürün sayısı (1–100). Query handler, değer gönderilmezse veya null ise 10 iletir.</param>
        /// <returns>Ürün satış özetleri; eşleşen satış yoksa boş liste.</returns>
        public async Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesAsync(int? year, int topCount, CancellationToken ct = default)
        { 
            const string sql = @"
            SELECT TOP (@TopCount)
                p.ProductID AS ProductId,
                p.ProductName,
                SUM(CAST(od.Quantity AS bigint)) AS TotalQuantity,
                COUNT(*) AS OrderCount,
                CAST(SUM(CAST(od.UnitPrice AS decimal(19, 4)) * od.Quantity
                    * (1 - CAST(od.Discount AS decimal(9, 6)))) AS decimal(19, 2)) AS NetSales
            FROM dbo.[Order Details] od
            INNER JOIN dbo.Orders o ON o.OrderID = od.OrderID
            INNER JOIN dbo.Products p ON p.ProductID = od.ProductID
            WHERE (@StartDate IS NULL OR o.OrderDate >= @StartDate)
                AND (@EndDate IS NULL OR o.OrderDate < @EndDate)
            GROUP BY p.ProductID, p.ProductName
            ORDER BY NetSales DESC, p.ProductID;";

            var parameters = new
            {
                TopCount = topCount,
                StartDate = year.HasValue ? new DateTime(year.Value, 1, 1) : (DateTime?)null,
                EndDate = year.HasValue && year.Value < 9999 ? new DateTime(year.Value + 1, 1, 1) : (DateTime?)null
            };

            var data = await unitOfWork.Connection.QueryAsync<ProductSalesReportItem>(new CommandDefinition(sql, parameters, unitOfWork.Transaction, cancellationToken: ct));
            return data.AsList();
        }

        /// <summary>
        /// Sipariş tarihine göre müşteri bazında satışları toplar ve net satış tutarı en yüksek müşterileri listeler.
        /// </summary>
        /// <remarks>
        /// İndirim sonrası net satışa kargo dahil değildir. Siparişler bir kez sayılır; satışı olmayan müşteriler dahil edilmez.
        /// Eşit net satış tutarlarında müşteri numarasına göre artan sıralanır. Yıl null ise tüm yıllar alınır.
        /// </remarks>
        public async Task<IReadOnlyList<CustomerSalesReportItem>> GetCustomerSalesAsync(int? year, int topCount, CancellationToken ct = default)
        {
            const string sql = @"
            SELECT TOP (@TopCount)
                c.CustomerID AS CustomerId,
                c.CompanyName,
                SUM(CAST(od.Quantity AS bigint)) AS TotalQuantity,
                COUNT(DISTINCT o.OrderID) AS OrderCount,
                CAST(SUM(CAST(od.UnitPrice AS decimal(19, 4)) * od.Quantity
                    * (1 - CAST(od.Discount AS decimal(9, 6)))) AS decimal(19, 2)) AS NetSales
            FROM dbo.[Order Details] od
            INNER JOIN dbo.Orders o ON o.OrderID = od.OrderID
            INNER JOIN dbo.Customers c ON c.CustomerID = o.CustomerID
            WHERE (@StartDate IS NULL OR o.OrderDate >= @StartDate)
                AND (@EndDate IS NULL OR o.OrderDate < @EndDate)
            GROUP BY c.CustomerID, c.CompanyName
            ORDER BY NetSales DESC, c.CustomerID;";

            var parameters = new
            {
                TopCount = topCount,
                StartDate = year.HasValue ? new DateTime(year.Value, 1, 1) : (DateTime?)null,
                EndDate = year.HasValue && year.Value < 9999 ? new DateTime(year.Value + 1, 1, 1) : (DateTime?)null
            };

            var data = await unitOfWork.Connection.QueryAsync<CustomerSalesReportItem>(new CommandDefinition(sql, parameters, unitOfWork.Transaction, cancellationToken: ct));
            return data.AsList();
        }

        /// <summary>
        /// Sipariş tarihine göre çalışan bazında satışları toplar ve net satış tutarı en yüksek çalışanları listeler.
        /// </summary>
        /// <remarks>
        /// İndirim sonrası net satışa kargo dahil değildir. Siparişler bir kez sayılır; satışı olmayan çalışanlar dahil edilmez.
        /// Eşit net satış tutarlarında çalışan numarasına göre artan sıralanır. Yıl null ise tüm yıllar alınır.
        /// </remarks>
        public async Task<IReadOnlyList<EmployeeSalesReportItem>> GetEmployeeSalesAsync(int? year, int topCount, CancellationToken ct = default)
        {
            const string sql = @"
            SELECT TOP (@TopCount)
                e.EmployeeID AS EmployeeId,
                e.FirstName,
                e.LastName,
                SUM(CAST(od.Quantity AS bigint)) AS TotalQuantity,
                COUNT(DISTINCT o.OrderID) AS OrderCount,
                CAST(SUM(CAST(od.UnitPrice AS decimal(19, 4)) * od.Quantity
                    * (1 - CAST(od.Discount AS decimal(9, 6)))) AS decimal(19, 2)) AS NetSales
            FROM dbo.[Order Details] od
            INNER JOIN dbo.Orders o ON o.OrderID = od.OrderID
            INNER JOIN dbo.Employees e ON e.EmployeeID = o.EmployeeID
            WHERE (@StartDate IS NULL OR o.OrderDate >= @StartDate)
                AND (@EndDate IS NULL OR o.OrderDate < @EndDate)
            GROUP BY e.EmployeeID, e.FirstName, e.LastName
            ORDER BY NetSales DESC, e.EmployeeID;";

            var parameters = new
            {
                TopCount = topCount,
                StartDate = year.HasValue ? new DateTime(year.Value, 1, 1) : (DateTime?)null,
                EndDate = year.HasValue && year.Value < 9999 ? new DateTime(year.Value + 1, 1, 1) : (DateTime?)null
            };

            var data = await unitOfWork.Connection.QueryAsync<EmployeeSalesReportItem>(new CommandDefinition(sql, parameters, unitOfWork.Transaction, cancellationToken: ct));
            return data.AsList();
        }
    }
}
