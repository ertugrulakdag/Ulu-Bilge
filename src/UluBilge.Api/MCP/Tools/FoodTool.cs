using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using UluBilge.Api.Models;
using UluBilge.App.Queries.FoodHandler;
using UluBilge.Core.Enums;
using UluBilge.Core.Queries;

namespace UluBilge.Api.MCP.Tools
{
    [McpServerToolType]
    public static class FoodTool
    {
        [Authorize(Roles = nameof(RoleEnum.Food) + "," + nameof(RoleEnum.Report) + "," + nameof(RoleEnum.Region) + "," + nameof(RoleEnum.Admin))]
        [McpServerTool(Name = "get_foods", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
        [Description("Şirketin öğle yemeği menüsünü sorgular. Türkçe veya İngilizce yemek/menü sorularında SQL/rapor tool'ları yerine bunu kullan. 'Bugün ne yemek var?' için day, 'bu hafta' için week, 'bu ay' için month gönder. 'Yarın' day/offset=1, 'gelecek hafta' week/offset=1, 'gelecek ay' month/offset=1; geçmiş dönemler için negatif offset kullan. Haftalar Pazartesi-Pazar, aylar takvim ayıdır; hafta sonları dahildir. Tarih belirtilmezse Turkey Standard Time saatine göre bugün kullanılır. Kullanıcının diline göre language=tr veya en gönder ve aynı dilde yanıtla. Sonuç örnek veridir: isSampleData ve note bilgisini belirt, onaylanmış şirket menüsü gibi sunma. / Retrieves company lunch menus for Turkish or English questions such as 'What is for lunch today?', 'tomorrow', 'this week' or 'next month'. Use period day/week/month and offset 0 for current, 1 for next, -1 for previous; omit date for server-local today. Weeks run Monday-Sunday, months are calendar months, weekends included. Choose language en for English, tr for Turkish; answer in the user's language. Disclose that the menus are sample data, not confirmed company menus.")]
        public static async Task<BaseResponse<GetFoodsAsync.Result>> GetFoodsAsync(
            IAsyncQueryHandler<GetFoodsAsync.Query, GetFoodsAsync.Result> queryHandler,
            [Description("Dönem / Period: day, week, month. Varsayılan / Default: day. Günlük/haftalık/aylık ve daily/weekly/monthly de kabul edilir.")] string period = "day",
            [Description("İstenen dönemin içindeki tarih, yyyy-MM-dd. Belirtilmezse Türkiye'de bugün. / A date within the requested period, yyyy-MM-dd; omitted or null means today in Turkey Standard Time.")] DateOnly? date = null,
            [Description("Dönem kaydırma (-366..366), varsayılan 0. day için gün, week için hafta, month için ay. date varsa ona, yoksa bugüne göre. / Period offset relative to date or today: days, weeks or months according to period. Default 0; 1 means next, -1 previous.")] int offset = 0,
            [Description("Kullanıcının dili / User's language: tr (Türkçe, varsayılan/default), en (English). Yemek adları bu dilde döner. / Dish names are returned in this language.")] string language = "tr",
            CancellationToken ct = default)
        {
            try
            {
                var result = await queryHandler.HandleAsync(new GetFoodsAsync.Query
                {
                    Period = period,
                    Date = date,
                    Offset = offset,
                    Language = language
                }, ct);
                return new(result);
            }
            catch (ValidationException exception)
            {
                throw new McpException(exception.Message, exception);
            }
        }
    }
}
