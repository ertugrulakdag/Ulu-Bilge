namespace UluBilge.Core.Models
{
    public sealed class CustomerSalesReportItem
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public long TotalQuantity { get; set; }
        public int OrderCount { get; set; }
        public decimal NetSales { get; set; }
    }
}
