namespace UluBilge.Core.Models
{
    public sealed class EmployeeSalesReportItem
    {
        public int EmployeeId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public long TotalQuantity { get; set; }
        public int OrderCount { get; set; }
        public decimal NetSales { get; set; }
    }
}
