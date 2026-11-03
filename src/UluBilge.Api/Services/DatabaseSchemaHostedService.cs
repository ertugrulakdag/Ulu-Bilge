using Microsoft.AspNetCore.Authorization;
using UluBilge.Data.Abstract;

namespace UluBilge.Api.Services
{
    public sealed class DatabaseSchemaHostedService(IDatabaseSchemaProvider schemaProvider, ILogger<DatabaseSchemaHostedService> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Hata başlangıcı durdurur; HTTP sunucusu şema hazırlandıktan sonra açılır.
            await schemaProvider.InitializeAsync(cancellationToken);
#if DEBUG
            logger.LogInformation("Rapor şeması yüklendi. Tablo sayısı: {TableCount} Versiyon: {Version}", schemaProvider.Snapshot.Tables.Count, schemaProvider.Snapshot.Version);
#endif
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
