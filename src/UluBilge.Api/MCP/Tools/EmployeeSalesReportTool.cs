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
//    public static class EmployeeSalesReportTool
//    {
//        [McpServerTool(Name = "get_employee_sales", ReadOnly = true, UseStructuredContent = true)]
//        [Description("İstek bu standart raporla karşılanabiliyorsa generic rapordan önce bunu kullan. Ciro, kâr veya tarih aralığı belirsizse kullanıcıya sor. Northwind siparişlerinden çalışan bazında satış raporu oluşturur ve indirim sonrası net satış tutarı en yüksek çalışanları listeler. Her çalışan için çalışan numarası, adı ve soyadı, toplam satılan adet, benzersiz sipariş sayısı ve net satış tutarı döndürülür. Kargo bedeli dahil değildir.")]
//        public static async Task<BaseResponse<GetEmployeeSalesAsync.Result>> GetEmployeeSalesAsync(
//            IAsyncQueryHandler<GetEmployeeSalesAsync.Query, GetEmployeeSalesAsync.Result> queryHandler,
//            [Description("Sipariş tarihine göre filtrelenecek yıl (1753 ile içinde bulunduğumuz yıl arasında). Gönderilmezse veya null ise tüm yıllar alınır.")] int? year = null,
//            [Description("Döndürülecek en fazla çalışan sayısı (1–100; yıl belirtilmezse en fazla 50). Gönderilmezse veya null ise 10 kullanılır.")] int? topCount = null,
//            CancellationToken ct = default)
//        {
//            try
//            {
//                var result = await queryHandler.HandleAsync(new GetEmployeeSalesAsync.Query
//                {
//                    Year = year,
//                    TopCount = topCount
//                }, ct);

//                return new BaseResponse<GetEmployeeSalesAsync.Result>(result);
//            }
//            catch (ValidationException exception)
//            {
//                throw new McpException(exception.Message, exception);
//            }
//        }
//    }
//}
