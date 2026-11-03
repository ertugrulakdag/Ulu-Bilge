using System.ComponentModel;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Authorization;
using UluBilge.Api.Models;
using UluBilge.Core.Models;
using UluBilge.Core.Enums;
using UluBilge.Data.Abstract;
using UluBilge.Data.Database;

namespace UluBilge.Api.MCP.Tools
{
    [McpServerToolType]
    public static class DatabaseSchemaTool
    {
        public sealed record Result(DatabaseSchemaSnapshot Schema, int MaxRows, int MaxResultBytes, int TimeoutSeconds);

        [Authorize(Roles = nameof(RoleEnum.Report) + "," + nameof(RoleEnum.Admin))]
        [McpServerTool(Name = "get_database_schema", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
        [Description("SQL hazırlamadan önce bu tool ile izinli SQL Server tablo ve viewlerini, sütunlarını, veri tiplerini, PK/FK ilişkilerini ve şema sürümünü al. Her sohbetin ilk generic raporundan önce çağır; şema sohbet bağlamındaysa aynı sürümü tekrar kullanabilirsin. Şema sürümü hatasında yeniden çağır. Şemada bulunmayan nesneleri kullanma; tablo ve view adlarının büyük/küçük harflerini aynen koru. Ciro/kâr gibi iş tanımları veya tarih aralığı belirsizse kullanıcıya sor. data.schema.tables içindeki tableOrViewType 1 ise tablo, 2 ise viewdir. View için PK/FK varsayma. Viewler başlangıçta tanım ve bağımlılık denetiminden geçer; dışlanan veya desteklenmeyen nesnelere erişen viewler sunulmaz. Synonym, UDF, hesaplanan sütun veya CLR tipi içeren fiziksel tablolar kapsam dışıdır. Bu tool yalnızca başlangıçta yüklenen değişmez şema snapshotını okur; değişiklikler uygulama yeniden başlatılınca görülür.")]
        public static BaseResponse<Result> GetDatabaseSchema(IDatabaseSchemaProvider schemaProvider, GenericReportOptions options)
        {
            return new(new(schemaProvider.Snapshot, options.MaxRows, options.MaxResultBytes, options.TimeoutSeconds));
        }
    }
}
