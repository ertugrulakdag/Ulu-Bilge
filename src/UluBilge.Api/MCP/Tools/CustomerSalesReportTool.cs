//using System.ComponentModel;
//using System.ComponentModel.DataAnnotations;
//using ModelContextProtocol;
//using ModelContextProtocol.Server;
//using UluBilge.Api.Models;
//using UluBilge.App.Queries.ReportHandler;
//using UluBilge.Core.Queries;

//namespace UluBilge.Api.MCP.Tools
//{
//    [McpServerToolType]
//    public static class CustomerSalesReportTool
//    {
//        [McpServerTool(Name = "get_customer_sales", ReadOnly = true, UseStructuredContent = true)]
//        [Description("İstek bu standart raporla karşılanabiliyorsa generic rapordan önce bunu kullan. Ciro, kâr veya tarih aralığı belirsizse kullanıcıya sor. Northwind siparişlerinden müşteri bazında satış raporu oluşturur ve indirim sonrası net satış tutarı en yüksek müşterileri listeler. Her müşteri için müşteri numarası ve firma adı, toplam satılan adet, benzersiz sipariş sayısı ve net satış tutarı döndürülür. Kargo bedeli dahil değildir.")]
//        public static async Task<BaseResponse<GetCustomerSalesAsync.Result>> GetCustomerSalesAsync(
//            IAsyncQueryHandler<GetCustomerSalesAsync.Query, GetCustomerSalesAsync.Result> queryHandler,
//            [Description("Sipariş tarihine göre filtrelenecek yıl (1753 ile içinde bulunduğumuz yıl arasında). Gönderilmezse veya null ise tüm yıllar alınır.")] int? year = null,
//            [Description("Döndürülecek en fazla müşteri sayısı (1–100; yıl belirtilmezse en fazla 50). Gönderilmezse veya null ise 10 kullanılır.")] int? topCount = null,
//            CancellationToken ct = default)
//        {
//            try
//            {
//                var result = await queryHandler.HandleAsync(new GetCustomerSalesAsync.Query
//                {
//                    Year = year,
//                    TopCount = topCount
//                }, ct);

//                return new BaseResponse<GetCustomerSalesAsync.Result>(result);
//            }
//            catch (ValidationException exception)
//            {
//                throw new McpException(exception.Message, exception);
//            }
//        }
//    }
//}
